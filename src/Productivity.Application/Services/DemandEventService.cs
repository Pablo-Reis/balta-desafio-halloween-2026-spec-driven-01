using Microsoft.EntityFrameworkCore;
using Productivity.Application.Persistence;
using Productivity.Domain;

namespace Productivity.Application.Services;

public interface IDemandEventService
{
    /// <summary>Fluxo cronológico de início, pausas, retomadas e conclusão (RF-07).</summary>
    Task<IReadOnlyList<DemandEvent>> ListByDemandAsync(Guid demandId);
}

public sealed class DemandEventService : IDemandEventService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DemandEventService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<DemandEvent>> ListByDemandAsync(Guid demandId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.DemandEvents.AsNoTracking()
            .Where(e => e.DemandId == demandId)
            .OrderBy(e => e.Sequence)
            .ToListAsync();
    }
}
