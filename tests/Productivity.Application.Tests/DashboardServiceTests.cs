using Microsoft.Extensions.Logging.Abstractions;
using Productivity.Application.Services;

namespace Productivity.Application.Tests;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly TestDbFactory _factory = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc) };
    private readonly DemandService _demands;
    private readonly DashboardService _dashboard;

    public DashboardServiceTests()
    {
        _demands = new DemandService(_factory, _clock, NullLogger<DemandService>.Instance);
        _dashboard = new DashboardService(_factory, _clock);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetLast30Days_WithoutData_ReturnsThirtyEmptyDays()
    {
        var data = await _dashboard.GetLast30DaysAsync();

        Assert.Equal(30, data.Days.Count);
        Assert.Equal(new DateTime(2026, 10, 29), data.Days[^1].Date);
        Assert.Equal(new DateTime(2026, 9, 30), data.Days[0].Date);
        Assert.Equal(TimeSpan.Zero, data.TotalWorked);
        Assert.Equal(0, data.DemandCount);
    }

    [Fact]
    public async Task GetLast30Days_SumsWorkedTimeIgnoringPauses()
    {
        await _demands.StartAsync("A", "Dev", null);
        _clock.Advance(TimeSpan.FromMinutes(60));
        await _demands.PauseAsync();
        _clock.Advance(TimeSpan.FromMinutes(30));
        await _demands.ResumeAsync();
        _clock.Advance(TimeSpan.FromMinutes(30));
        await _demands.FinishAsync();

        var data = await _dashboard.GetLast30DaysAsync();

        Assert.Equal(TimeSpan.FromMinutes(90), data.TotalWorked);
        Assert.Equal(TimeSpan.FromMinutes(90), data.Days[^1].Worked);
        Assert.Equal(1, data.DemandCount);
        Assert.Equal(TimeSpan.FromMinutes(3), data.AveragePerDay);
    }

    [Fact]
    public async Task GetLast30Days_GroupsByTypeOrderedByTime()
    {
        await _demands.StartAsync("A", "Reunião", null);
        _clock.Advance(TimeSpan.FromMinutes(30));
        await _demands.StartAsync("B", "Dev", null, finishCurrent: true);
        _clock.Advance(TimeSpan.FromMinutes(120));
        await _demands.StartAsync("C", "reunião", null, finishCurrent: true);
        _clock.Advance(TimeSpan.FromMinutes(15));
        await _demands.FinishAsync();

        var data = await _dashboard.GetLast30DaysAsync();

        Assert.Equal(2, data.ByType.Count);
        Assert.Equal("Dev", data.ByType[0].Type);
        Assert.Equal(TimeSpan.FromMinutes(120), data.ByType[0].Worked);
        Assert.Equal(TimeSpan.FromMinutes(45), data.ByType[1].Worked);
    }

    [Fact]
    public async Task GetLast30Days_SplitsWorkAcrossMidnight()
    {
        _clock.UtcNow = new DateTime(2026, 10, 28, 23, 0, 0, DateTimeKind.Utc);
        await _demands.StartAsync("Noturna", "Dev", null);
        _clock.Advance(TimeSpan.FromHours(3));
        await _demands.FinishAsync();

        var data = await _dashboard.GetLast30DaysAsync();

        Assert.Equal(TimeSpan.FromHours(1), data.Days.Single(d => d.Date == new DateTime(2026, 10, 28)).Worked);
        Assert.Equal(TimeSpan.FromHours(2), data.Days.Single(d => d.Date == new DateTime(2026, 10, 29)).Worked);
    }

    [Fact]
    public async Task GetLast30Days_IncludesRunningDemandUntilNow()
    {
        await _demands.StartAsync("A", "Dev", null);
        _clock.Advance(TimeSpan.FromMinutes(45));

        var data = await _dashboard.GetLast30DaysAsync();

        Assert.Equal(TimeSpan.FromMinutes(45), data.TotalWorked);
    }

    [Fact]
    public async Task GetLast30Days_IgnoresWorkOlderThanThirtyDays()
    {
        _clock.UtcNow = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        await _demands.StartAsync("Antiga", "Dev", null);
        _clock.Advance(TimeSpan.FromHours(2));
        await _demands.FinishAsync();
        _clock.UtcNow = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

        var data = await _dashboard.GetLast30DaysAsync();

        Assert.Equal(TimeSpan.Zero, data.TotalWorked);
        Assert.Equal(0, data.DemandCount);
    }
}
