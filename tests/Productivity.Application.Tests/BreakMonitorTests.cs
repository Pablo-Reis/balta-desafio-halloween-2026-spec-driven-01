using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.Application.Tests;

public class BreakMonitorTests
{
    private readonly FakeClock _clock = new();
    private readonly UserSettings _settings = UserSettings.Default with { BreakAlertMinutes = 50, BreakDurationMinutes = 10 };

    [Fact]
    public void Evaluate_WithoutActiveDemand_ReturnsNone()
    {
        Assert.Equal(BreakAlert.None, new BreakMonitor(_clock).Evaluate(null, _settings));
    }

    [Fact]
    public void Evaluate_BeforeThreshold_ReturnsNone()
    {
        var demand = Demand.Start("x", "Dev", null, _clock.UtcNow);
        var monitor = new BreakMonitor(_clock);
        _clock.Advance(TimeSpan.FromMinutes(49));

        Assert.Equal(BreakAlert.None, monitor.Evaluate(demand, _settings));
    }

    [Fact]
    public void Evaluate_AfterWorkThreshold_AlertsOnlyOnceAndDoesNotChangeDemand()
    {
        var demand = Demand.Start("x", "Dev", null, _clock.UtcNow);
        var monitor = new BreakMonitor(_clock);
        _clock.Advance(TimeSpan.FromMinutes(50));

        Assert.Equal(BreakAlert.TakeBreak, monitor.Evaluate(demand, _settings));
        Assert.Equal(BreakAlert.None, monitor.Evaluate(demand, _settings));
        Assert.Equal(DemandStatus.Running, demand.Status); // RF-12: não interrompe
    }

    [Fact]
    public void Evaluate_AfterRestThreshold_AlertsReturnOnlyOnce()
    {
        var demand = Demand.Start("x", "Dev", null, _clock.UtcNow);
        var monitor = new BreakMonitor(_clock);
        _clock.Advance(TimeSpan.FromMinutes(20));
        demand.Pause(_clock.UtcNow);
        _clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(BreakAlert.ReturnToWork, monitor.Evaluate(demand, _settings));
        Assert.Equal(BreakAlert.None, monitor.Evaluate(demand, _settings));
        Assert.Equal(DemandStatus.Paused, demand.Status); // RF-12: não retoma sozinho
    }

    [Fact]
    public void Evaluate_AfterResume_StartsNewWorkCycle()
    {
        var demand = Demand.Start("x", "Dev", null, _clock.UtcNow);
        var monitor = new BreakMonitor(_clock);
        _clock.Advance(TimeSpan.FromMinutes(50));
        Assert.Equal(BreakAlert.TakeBreak, monitor.Evaluate(demand, _settings));

        demand.Pause(_clock.UtcNow);
        demand.Resume(_clock.UtcNow);
        _clock.Advance(TimeSpan.FromMinutes(50));

        Assert.Equal(BreakAlert.TakeBreak, monitor.Evaluate(demand, _settings));
    }

    [Fact]
    public void Evaluate_WithZeroMinutes_NeverAlerts()
    {
        var demand = Demand.Start("x", "Dev", null, _clock.UtcNow);
        var monitor = new BreakMonitor(_clock);
        var disabled = _settings with { BreakAlertMinutes = 0, BreakDurationMinutes = 0 };
        _clock.Advance(TimeSpan.FromHours(10));

        Assert.Equal(BreakAlert.None, monitor.Evaluate(demand, disabled));
        demand.Pause(_clock.UtcNow);
        _clock.Advance(TimeSpan.FromHours(10));
        Assert.Equal(BreakAlert.None, monitor.Evaluate(demand, disabled));
    }
}
