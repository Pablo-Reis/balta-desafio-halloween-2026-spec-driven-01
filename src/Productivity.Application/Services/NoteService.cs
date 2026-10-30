using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Productivity.Application.Abstractions;
using Productivity.Application.Persistence;
using Productivity.Domain;

namespace Productivity.Application.Services;

public interface INoteService
{
    Task<Note> AddAsync(Guid demandId, string text, NoteSource source);

    /// <summary>Adiciona a anotação à demanda ativa (em andamento ou pausada).</summary>
    Task<Note> AddToActiveAsync(string text, NoteSource source);

    Task<IReadOnlyList<Note>> ListByDemandAsync(Guid demandId);
}

public sealed class NoteService : INoteService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IClock _clock;
    private readonly ILogger<NoteService> _logger;

    public NoteService(IDbContextFactory<AppDbContext> factory, IClock clock, ILogger<NoteService> logger)
    {
        _factory = factory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Note> AddAsync(Guid demandId, string text, NoteSource source)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (!await db.Demands.AnyAsync(d => d.Id == demandId))
            throw new DomainException("Demanda não encontrada.");

        return await SaveAsync(db, demandId, text, source);
    }

    public async Task<Note> AddToActiveAsync(string text, NoteSource source)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var activeId = await db.Demands
            .Where(d => d.Status != DemandStatus.Finished)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        if (activeId is null)
            throw new DomainException("Não há nenhuma demanda em andamento para receber a anotação.");

        return await SaveAsync(db, activeId.Value, text, source);
    }

    public async Task<IReadOnlyList<Note>> ListByDemandAsync(Guid demandId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var notes = await db.Notes.AsNoTracking().Where(n => n.DemandId == demandId).ToListAsync();
        return notes.OrderBy(n => n.CreatedAt).ToList();
    }

    private async Task<Note> SaveAsync(AppDbContext db, Guid demandId, string text, NoteSource source)
    {
        var note = Note.Create(demandId, text, source, _clock.UtcNow);
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        _logger.LogInformation("Anotação {NoteId} adicionada à demanda {DemandId}.", note.Id, demandId);
        return note;
    }
}
