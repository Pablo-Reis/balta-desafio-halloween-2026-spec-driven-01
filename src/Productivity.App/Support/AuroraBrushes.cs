using System.Windows;
using System.Windows.Media;

namespace Productivity.App.Support;

/// <summary>Gradientes por tipo de demanda (o primeiro segue o acento Aurora).</summary>
public static class AuroraBrushes
{
    private static readonly (string From, string To)[] Pairs =
    {
        ("#8B5CF6", "#22D3EE"),
        ("#F472B6", "#FB923C"),
        ("#34D399", "#22D3EE"),
        ("#FBBF24", "#F97316"),
        ("#60A5FA", "#818CF8"),
        ("#A78BFA", "#F472B6")
    };

    public static Brush TypeBrush(int index)
    {
        var (from, to) = Pairs[index % Pairs.Length];
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(from),
            (Color)ColorConverter.ConvertFromString(to),
            new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }

    public static Brush Resource(string key) => (Brush)System.Windows.Application.Current.FindResource(key);
}
