using Microsoft.EntityFrameworkCore;
using Productivity.Application.Abstractions;
using Productivity.Application.Persistence;
using Productivity.Domain;

namespace Productivity.Application.Services;

public sealed record DayProductivity(DateTime Date, TimeSpan Worked);

public sealed record TypeProductivity(string Type, TimeSpan Worked);

public sealed record DashboardData(
    IReadOnlyList<DayProductivity> Days,
    IReadOnlyList<TypeProductivity> ByType,
    TimeSpan TotalWorked,
    TimeSpan AveragePerDay,
    int DemandCount);

public interface IDashboardService
{
    Task<DashboardData> GetLast30DaysAsync();
}

public sealed class DashboardService : IDashboardService
{
    public const int WindowDays = 30;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IClock _clock;

    public DashboardService(IDbContextFactory<AppDbContext> factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task<DashboardData> GetLast30DaysAsync()
    {
        var now = _clock.UtcNow;
        var tz = _clock.TimeZone;
        var today = TimeZoneInfo.ConvertTimeFromUtc(now, tz).Date;
        var windowStart = today.AddDays(-(WindowDays - 1));
        var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(windowStart, DateTimeKind.Unspecified), tz);

        await using var db = await _factory.CreateDbContextAsync();
        var demands = await db.Demands.AsNoTracking().Include(d => d.Events)
            .Where(d => d.Status != DemandStatus.Finished || d.FinishedAt >= windowStartUtc)
            .ToListAsync();

        var perDay = Enumerable.Range(0, WindowDays).ToDictionary(i => windowStart.AddDays(i), _ => TimeSpan.Zero);
        var perType = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        var counted = 0;

        foreach (var demand in demands)
        {
            var demandTotal = TimeSpan.Zero;

            foreach (var segment in demand.GetWorkSegments(now))
            {
                var start = TimeZoneInfo.ConvertTimeFromUtc(segment.Start, tz);
                var end = TimeZoneInfo.ConvertTimeFromUtc(segment.End, tz);

                for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
                {
                    if (!perDay.ContainsKey(day))
                        continue;

                    var from = start > day ? start : day;
                    var to = end < day.AddDays(1) ? end : day.AddDays(1);
                    if (to <= from)
                        continue;

                    perDay[day] += to - from;
                    demandTotal += to - from;
                }
            }

            if (demandTotal > TimeSpan.Zero)
            {
                counted++;
                perType[demand.Type] = perType.GetValueOrDefault(demand.Type) + demandTotal;
            }
        }

        var total = perDay.Values.Aggregate(TimeSpan.Zero, (sum, t) => sum + t);
        return new DashboardData(
            perDay.OrderBy(p => p.Key).Select(p => new DayProductivity(p.Key, p.Value)).ToList(),
            perType.OrderByDescending(p => p.Value).Select(p => new TypeProductivity(p.Key, p.Value)).ToList(),
            total,
            total / WindowDays,
            counted);
    }
}
