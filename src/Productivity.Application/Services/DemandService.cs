using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Productivity.Application.Abstractions;
using Productivity.Application.Persistence;
using Productivity.Domain;

namespace Productivity.Application.Services;

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
