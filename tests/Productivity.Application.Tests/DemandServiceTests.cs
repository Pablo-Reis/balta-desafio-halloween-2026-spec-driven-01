using Microsoft.Extensions.Logging.Abstractions;
using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.Application.Tests;

public sealed class DemandServiceTests : IDisposable
{
    private readonly TestDbFactory _factory = new();
    private readonly FakeClock _clock = new();
    private readonly DemandService _service;

    public DemandServiceTests()
    {
        _service = new DemandService(_factory, _clock, NullLogger<DemandService>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Start_PersistsRunningDemand()
    {
        var demand = await _service.StartAsync("Corrigir bug", "Desenvolvimento", null);

        var active = await _service.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(demand.Id, active.Id);
        Assert.Equal(DemandStatus.Running, active.Status);
        Assert.Single(active.Events);
    }

    [Fact]
    public async Task Start_WithActiveDemand_ThrowsUnlessFinishCurrent()
    {
        await _service.StartAsync("A", "Dev", null);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.StartAsync("B", "Dev", null));
        Assert.Contains("Já existe uma demanda em andamento", ex.Message);
    }

    [Fact]
    public async Task Start_WithFinishCurrent_FinishesPreviousAndStartsNew()
    {
        var first = await _service.StartAsync("A", "Dev", null);
        _clock.Advance(TimeSpan.FromMinutes(20));

        var second = await _service.StartAsync("B", "Dev", null, finishCurrent: true);

        var reloaded = await _service.GetByIdAsync(first.Id);
        Assert.Equal(DemandStatus.Finished, reloaded!.Status);
        Assert.Equal(TimeSpan.FromMinutes(20), reloaded.GetWorkedTime(_clock.UtcNow));
        Assert.Equal(second.Id, (await _service.GetActiveAsync())!.Id);
    }

    [Fact]
    public async Task Start_WithInvalidData_DoesNotFinishCurrent()
    {
        var first = await _service.StartAsync("A", "Dev", null);

        await Assert.ThrowsAsync<DomainException>(() => _service.StartAsync("", "Dev", null, finishCurrent: true));

        Assert.Equal(first.Id, (await _service.GetActiveAsync())!.Id);
    }

    [Fact]
    public async Task PauseResume_ExcludesPauseFromWorkedTime()
    {
        var demand = await _service.StartAsync("A", "Dev", null);
        _clock.Advance(TimeSpan.FromMinutes(10));
        await _service.PauseAsync();
        _clock.Advance(TimeSpan.FromMinutes(30));
        await _service.ResumeAsync();
        _clock.Advance(TimeSpan.FromMinutes(5));
        await _service.FinishAsync();

        var reloaded = await _service.GetByIdAsync(demand.Id);
        Assert.Equal(TimeSpan.FromMinutes(15), reloaded!.GetWorkedTime(_clock.UtcNow));
        Assert.Equal(TimeSpan.FromMinutes(30), reloaded.GetPausedTime(_clock.UtcNow));
        Assert.Equal(4, reloaded.Events.Count);
    }

    [Fact]
    public async Task TogglePause_AlternatesBetweenPausedAndRunning()
    {
        await _service.StartAsync("A", "Dev", null);

        Assert.Equal(DemandStatus.Paused, (await _service.TogglePauseAsync()).Status);
        Assert.Equal(DemandStatus.Running, (await _service.TogglePauseAsync()).Status);
    }

    [Fact]
    public async Task PauseFinishResume_WithoutActiveDemand_Throw()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.PauseAsync());
        await Assert.ThrowsAsync<DomainException>(() => _service.ResumeAsync());
        await Assert.ThrowsAsync<DomainException>(() => _service.FinishAsync());
        await Assert.ThrowsAsync<DomainException>(() => _service.TogglePauseAsync());
    }

    [Fact]
    public async Task GetActive_ReturnsPausedDemandAfterRestart()
    {
        await _service.StartAsync("A", "Dev", null);
        await _service.PauseAsync();

        // Novo service sobre o mesmo banco simula reabrir o programa (D-14).
        var restarted = new DemandService(_factory, _clock, NullLogger<DemandService>.Instance);
        var active = await restarted.GetActiveAsync();

        Assert.Equal(DemandStatus.Paused, active!.Status);
    }

    [Fact]
    public async Task GetActive_WithoutDemands_ReturnsNull()
    {
        Assert.Null(await _service.GetActiveAsync());
    }

    [Fact]
    public async Task UpdateDetails_PersistsChanges()
    {
        var demand = await _service.StartAsync("A", "Dev", null);
        await _service.FinishAsync();

        await _service.UpdateDetailsAsync(demand.Id, "A editada", "Reunião", "mais detalhes");

        var reloaded = await _service.GetByIdAsync(demand.Id);
        Assert.Equal("A editada", reloaded!.Title);
        Assert.Equal("Reunião", reloaded.Type);
        Assert.Equal("mais detalhes", reloaded.Description);
    }

    [Fact]
    public async Task UpdateDetails_WithUnknownId_Throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.UpdateDetailsAsync(Guid.NewGuid(), "a", "b", null));
    }

    [Fact]
    public async Task List_ReturnsNewestFirst_AndTypesAreDistinct()
    {
        await _service.StartAsync("A", "Dev", null);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _service.StartAsync("B", "Reunião", null, finishCurrent: true);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _service.StartAsync("C", "Dev", null, finishCurrent: true);

        var list = await _service.ListAsync();

        Assert.Equal(new[] { "C", "B", "A" }, list.Select(d => d.Title));
        Assert.Equal(new[] { "Dev", "Reunião" }, await _service.ListTypesAsync());
    }

    [Fact]
    public async Task Changes_RaiseDemandsChanged()
    {
        var raised = 0;
        _service.DemandsChanged += () => raised++;

        await _service.StartAsync("A", "Dev", null);
        await _service.PauseAsync();
        await _service.FinishAsync();

        Assert.Equal(3, raised);
    }
}
