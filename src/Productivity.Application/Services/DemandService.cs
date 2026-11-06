using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Productivity.Application.Abstractions;
using Productivity.Application.Persistence;
using Productivity.Domain;

namespace Productivity.Application.Services;

/// <summary>Filtros do histórico. Datas são dias do calendário local (início da demanda), com os dois extremos incluídos.</summary>
public sealed record DemandFilter(string? Text = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>Demandas iniciadas em um mesmo dia e o total trabalhado nelas.</summary>
public sealed record DemandDayGroup(DateOnly Day, IReadOnlyList<Demand> Demands, TimeSpan Worked);

public interface IDemandService
{
    /// <summary>Disparado após qualquer mudança em demandas (início, pausa, retomada, fim, edição).</summary>
    event Action? DemandsChanged;

    Task<Demand> StartAsync(string title, string type, string? description, bool finishCurrent = false);

    Task<Demand> PauseAsync();

    Task<Demand> ResumeAsync();

    /// <summary>Pausa se estiver em andamento, retoma se estiver pausada.</summary>
    Task<Demand> TogglePauseAsync();

    Task<Demand> FinishAsync();

    Task<Demand?> GetActiveAsync();

    Task<Demand?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<Demand>> ListAsync(int take = 200);

    /// <summary>Histórico filtrado por título e/ou data, agrupado por dia (mais recente primeiro).</summary>
    Task<IReadOnlyList<DemandDayGroup>> SearchAsync(DemandFilter filter, int take = 500);

    Task<Demand> UpdateDetailsAsync(Guid id, string title, string type, string? description);

    Task<IReadOnlyList<string>> ListTypesAsync();
}

public sealed class DemandService : IDemandService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IClock _clock;
    private readonly ILogger<DemandService> _logger;

    public DemandService(IDbContextFactory<AppDbContext> factory, IClock clock, ILogger<DemandService> logger)
    {
        _factory = factory;
        _clock = clock;
        _logger = logger;
    }

    public event Action? DemandsChanged;

    public async Task<Demand> StartAsync(string title, string type, string? description, bool finishCurrent = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var active = await LoadActiveAsync(db);
        var now = _clock.UtcNow;

        // Valida antes de finalizar a anterior para não perder a demanda em andamento por dados inválidos.
        var demand = Demand.Start(title, type, description, now);

        if (active is not null)
        {
            if (!finishCurrent)
                throw new DomainException("Já existe uma demanda em andamento. Finalize-a antes de iniciar outra.");

            active.Finish(now);
        }

        db.Demands.Add(demand);
        await db.SaveChangesAsync();
        _logger.LogInformation("Demanda {DemandId} iniciada.", demand.Id);
        DemandsChanged?.Invoke();
        return demand;
    }

    public Task<Demand> PauseAsync() => ChangeActiveAsync(d => d.Pause(_clock.UtcNow), "pausada");

    public Task<Demand> ResumeAsync() => ChangeActiveAsync(d => d.Resume(_clock.UtcNow), "retomada");

    public Task<Demand> TogglePauseAsync() => ChangeActiveAsync(
        d =>
        {
            if (d.Status == DemandStatus.Running)
                d.Pause(_clock.UtcNow);
            else
                d.Resume(_clock.UtcNow);
        },
        "alternada entre trabalho e pausa");

    public Task<Demand> FinishAsync() => ChangeActiveAsync(d => d.Finish(_clock.UtcNow), "finalizada");

    public async Task<Demand?> GetActiveAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await LoadActiveAsync(db, track: false);
    }

    public async Task<Demand?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Demands.AsNoTracking().Include(d => d.Events).FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<IReadOnlyList<Demand>> ListAsync(int take = 200)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Demands.AsNoTracking().Include(d => d.Events)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DemandDayGroup>> SearchAsync(DemandFilter filter, int take = 500)
    {
        if (filter.From is { } from && filter.To is { } to && from > to)
            throw new DomainException("A data inicial não pode ser maior que a data final.");

        var zone = _clock.TimeZone;
        var now = _clock.UtcNow;

        await using var db = await _factory.CreateDbContextAsync();
        IQueryable<Demand> query = db.Demands.AsNoTracking().Include(d => d.Events);

        if (filter.From is { } start)
        {
            var startUtc = LocalMidnightToUtc(start, zone);
            query = query.Where(d => d.CreatedAt >= startUtc);
        }

        if (filter.To is { } end)
        {
            var endUtc = LocalMidnightToUtc(end.AddDays(1), zone);
            query = query.Where(d => d.CreatedAt < endUtc);
        }

        var demands = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();

        // Busca por título sem diferenciar maiúsculas/minúsculas nem acentos ("integracao" acha "Integração").
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = filter.Text.Trim();
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            demands = demands
                .Where(d => compare.IndexOf(d.Title, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                .ToList();
        }

        return demands
            .Take(take)
            .GroupBy(d => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(d.CreatedAt, zone)))
            .OrderByDescending(g => g.Key)
            .Select(g => new DemandDayGroup(
                g.Key,
                g.ToList(),
                g.Aggregate(TimeSpan.Zero, (sum, d) => sum + d.GetWorkedTime(now))))
            .ToList();
    }

    public async Task<Demand> UpdateDetailsAsync(Guid id, string title, string type, string? description)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var demand = await db.Demands.Include(d => d.Events).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new DomainException("Demanda não encontrada.");

        demand.UpdateDetails(title, type, description);
        await db.SaveChangesAsync();
        _logger.LogInformation("Demanda {DemandId} atualizada.", id);
        DemandsChanged?.Invoke();
        return demand;
    }

    public async Task<IReadOnlyList<string>> ListTypesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Demands.AsNoTracking().Select(d => d.Type).Distinct().OrderBy(t => t).ToListAsync();
    }

    private static DateTime LocalMidnightToUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
            local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private async Task<Demand> ChangeActiveAsync(Action<Demand> change, string logAction)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var demand = await LoadActiveAsync(db) ?? throw new DomainException("Não há nenhuma demanda em andamento.");

        change(demand);
        await db.SaveChangesAsync();
        _logger.LogInformation("Demanda {DemandId} {Action}.", demand.Id, logAction);
        DemandsChanged?.Invoke();
        return demand;
    }

    private static Task<Demand?> LoadActiveAsync(AppDbContext db, bool track = true)
    {
        var query = db.Demands.Include(d => d.Events).Where(d => d.Status != DemandStatus.Finished);
        if (!track)
            query = query.AsNoTracking();
        return query.OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync();
    }
}

