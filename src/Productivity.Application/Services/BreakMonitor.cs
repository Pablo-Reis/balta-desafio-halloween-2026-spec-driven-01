using Productivity.Application.Abstractions;
using Productivity.Domain;

namespace Productivity.Application.Services;

public enum BreakAlert
{
    None,
    TakeBreak,
    ReturnToWork
}

/// <summary>
/// Decide quando avisar o descanso (RF-09) e a volta ao trabalho (RF-10).
/// Apenas avisa: nunca altera a demanda (RF-12). Cada aviso é emitido uma única vez por trecho.
/// </summary>
public sealed class BreakMonitor
{
    private readonly IClock _clock;
    private (Guid DemandId, int Sequence)? _lastAlerted;

    public BreakMonitor(IClock clock)
    {
        _clock = clock;
    }

    public BreakAlert Evaluate(Demand? active, UserSettings settings)
    {
        if (active is null || active.Status == DemandStatus.Finished)
        {
            _lastAlerted = null;
            return BreakAlert.None;
        }

        var last = active.LastEvent;
        var marker = (active.Id, last.Sequence);
        if (_lastAlerted == marker)
            return BreakAlert.None;

        var elapsed = active.GetTimeSinceLastEvent(_clock.UtcNow);

        var alert = active.Status switch
        {
            DemandStatus.Running when settings.BreakAlertMinutes > 0 && elapsed >= TimeSpan.FromMinutes(settings.BreakAlertMinutes) => BreakAlert.TakeBreak,
            DemandStatus.Paused when settings.BreakDurationMinutes > 0 && elapsed >= TimeSpan.FromMinutes(settings.BreakDurationMinutes) => BreakAlert.ReturnToWork,
            _ => BreakAlert.None
        };

        if (alert != BreakAlert.None)
            _lastAlerted = marker;

        return alert;
    }
}
