namespace Productivity.Domain;

/// <summary>Intervalo contínuo de trabalho (sem pausas).</summary>
public readonly record struct WorkSegment(DateTime Start, DateTime End)
{
    public TimeSpan Duration => End - Start;
}
