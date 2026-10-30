using Microsoft.Extensions.Logging.Abstractions;
using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.Application.Tests;

public sealed class NoteAndEventServiceTests : IDisposable
{
    private readonly TestDbFactory _factory = new();
    private readonly FakeClock _clock = new();
    private readonly DemandService _demands;
    private readonly NoteService _notes;
    private readonly DemandEventService _events;

    public NoteAndEventServiceTests()
    {
        _demands = new DemandService(_factory, _clock, NullLogger<DemandService>.Instance);
        _notes = new NoteService(_factory, _clock, NullLogger<NoteService>.Instance);
        _events = new DemandEventService(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task AddToActive_SavesNoteOnActiveDemand()
    {
        var demand = await _demands.StartAsync("A", "Dev", null);

        await _notes.AddToActiveAsync("  lembrar de testar  ", NoteSource.Hotkey);

        var notes = await _notes.ListByDemandAsync(demand.Id);
        var note = Assert.Single(notes);
        Assert.Equal("lembrar de testar", note.Text);
        Assert.Equal(NoteSource.Hotkey, note.Source);
    }

    [Fact]
    public async Task AddToActive_WorksWhilePaused()
    {
        var demand = await _demands.StartAsync("A", "Dev", null);
        await _demands.PauseAsync();

        await _notes.AddToActiveAsync("durante a pausa", NoteSource.Hotkey);

        Assert.Single(await _notes.ListByDemandAsync(demand.Id));
    }

    [Fact]
    public async Task AddToActive_WithoutActiveDemand_Throws()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => _notes.AddToActiveAsync("x", NoteSource.Hotkey));
        Assert.Contains("nenhuma demanda em andamento", ex.Message);
    }

    [Fact]
    public async Task Add_ToFinishedDemand_IsAllowed()
    {
        var demand = await _demands.StartAsync("A", "Dev", null);
        await _demands.FinishAsync();

        await _notes.AddAsync(demand.Id, "revisão posterior", NoteSource.Application);

        Assert.Single(await _notes.ListByDemandAsync(demand.Id));
    }

    [Fact]
    public async Task Add_WithEmptyText_Throws()
    {
        var demand = await _demands.StartAsync("A", "Dev", null);

        await Assert.ThrowsAsync<DomainException>(() => _notes.AddAsync(demand.Id, "   ", NoteSource.Application));
    }

    [Fact]
    public async Task Add_WithUnknownDemand_Throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _notes.AddAsync(Guid.NewGuid(), "x", NoteSource.Application));
    }

    [Fact]
    public async Task Notes_AreListedInChronologicalOrder()
    {
        var demand = await _demands.StartAsync("A", "Dev", null);
        await _notes.AddAsync(demand.Id, "primeira", NoteSource.Application);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _notes.AddAsync(demand.Id, "segunda", NoteSource.Application);

        var notes = await _notes.ListByDemandAsync(demand.Id);

        Assert.Equal(new[] { "primeira", "segunda" }, notes.Select(n => n.Text));
    }

    [Fact]
    public async Task ListByDemand_ReturnsChronologicalFlowWithTimes()
    {
        var demand = await _demands.StartAsync("A", "Dev", null);
        var started = _clock.UtcNow;
        _clock.Advance(TimeSpan.FromSeconds(95));
        await _demands.PauseAsync();
        _clock.Advance(TimeSpan.FromSeconds(30));
        await _demands.ResumeAsync();
        _clock.Advance(TimeSpan.FromSeconds(10));
        await _demands.FinishAsync();

        var events = await _events.ListByDemandAsync(demand.Id);

        Assert.Equal(
            new[] { DemandEventKind.Started, DemandEventKind.Paused, DemandEventKind.Resumed, DemandEventKind.Finished },
            events.Select(e => e.Kind));
        Assert.Equal(started.AddSeconds(95), events[1].OccurredAt);
        Assert.Equal(started.AddSeconds(125), events[2].OccurredAt);
        Assert.All(events, e => Assert.Equal(DateTimeKind.Utc, e.OccurredAt.Kind));
    }
}
