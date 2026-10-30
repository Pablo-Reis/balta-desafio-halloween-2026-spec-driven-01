using System.Globalization;
using Productivity.Domain;

namespace Productivity.App.Support;

public static class Format
{
    private static readonly CultureInfo Culture = new("pt-BR");

    /// <summary>HH:mm:ss (horas podem passar de 99).</summary>
    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    /// <summary>Ex.: "2h 05min", "35min", "42s".</summary>
    public static string Short(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes:00}min";
        if (span.TotalMinutes >= 1)
            return $"{span.Minutes}min";
        return $"{span.Seconds}s";
    }

    /// <summary>Divide a duração em valor e unidade para exibição em cartões (ex.: "4" + "min").</summary>
    public static (string Value, string Unit) ShortParts(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        if (span.TotalHours >= 1)
            return ($"{(int)span.TotalHours}h {span.Minutes:00}", "min");
        if (span.TotalMinutes >= 1)
            return (span.Minutes.ToString(), "min");
        return (span.Seconds.ToString(), "s");
    }

    public static string DateTimeSeconds(DateTime utc) =>
        utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", Culture);

    public static string DateTimeMinutes(DateTime utc) =>
        utc.ToLocalTime().ToString("dd/MM HH:mm", Culture);

    public static string EventLabel(DemandEventKind kind) => kind switch
    {
        DemandEventKind.Started => "Início",
        DemandEventKind.Paused => "Pausa (descanso)",
        DemandEventKind.Resumed => "Retomada",
        DemandEventKind.Finished => "Conclusão",
        _ => kind.ToString()
    };

    public static string StatusLabel(DemandStatus status) => status switch
    {
        DemandStatus.Running => "Trabalhando",
        DemandStatus.Paused => "Descansando",
        _ => "Finalizada"
    };
}

