using System.Windows.Media.Animation;

namespace Productivity.App.Support;

/// <summary>Easing equivalente ao cubic-bezier(x1, y1, x2, y2) do CSS.</summary>
public sealed class CubicBezierEase : IEasingFunction
{
    private readonly double _x1, _y1, _x2, _y2;

    public CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    /// <summary>cubic-bezier(.22, 1, .36, 1): entrada suave dos cartões.</summary>
    public static CubicBezierEase Rise { get; } = new(0.22, 1, 0.36, 1);

    /// <summary>cubic-bezier(.32, .72, 0, 1): transformação estilo Dynamic Island.</summary>
    public static CubicBezierEase Island { get; } = new(0.32, 0.72, 0, 1);

    public double Ease(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;

        // Encontra t tal que x(t) = tempo normalizado (bissecção) e devolve y(t).
        double lo = 0, hi = 1, t = normalizedTime;
        for (var i = 0; i < 24; i++)
        {
            t = (lo + hi) / 2;
            if (Bezier(t, _x1, _x2) < normalizedTime) lo = t; else hi = t;
        }

        return Bezier(t, _y1, _y2);
    }

    private static double Bezier(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }
}
