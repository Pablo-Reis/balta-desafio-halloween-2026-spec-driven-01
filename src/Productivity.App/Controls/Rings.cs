using System.Windows;
using System.Windows.Media;

namespace Productivity.App.Controls;

internal static class ArcDrawing
{
    /// <summary>Gradientes passam a usar coordenadas absolutas para não "andarem" conforme o arco cresce.</summary>
    public static Brush Absolute(Brush brush, double size)
    {
        if (brush is not LinearGradientBrush gradient)
            return brush;

        var clone = gradient.Clone();
        clone.MappingMode = BrushMappingMode.Absolute;
        clone.StartPoint = new Point(0, 0);
        clone.EndPoint = new Point(size, size);
        return clone;
    }

    /// <summary>Desenha um arco começando no topo (12h) em sentido horário.</summary>
    public static void Draw(DrawingContext dc, Pen pen, Point center, double radius, double startDegrees, double sweepDegrees)
    {
        if (sweepDegrees >= 359.99)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        if (sweepDegrees <= 0.01)
            return;

        static Point At(Point c, double r, double degrees)
        {
            var rad = (degrees - 90) * Math.PI / 180;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(center, radius, startDegrees), false, false);
            ctx.ArcTo(At(center, radius, startDegrees + sweepDegrees), new Size(radius, radius), 0,
                sweepDegrees > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}

/// <summary>Anel de progresso com gradiente (0 a 1) e pontas arredondadas.</summary>
public sealed class RingProgress : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(RingProgress),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(RingProgress),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(RingProgress),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(RingProgress),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness)
            return;

        var center = new Point(size / 2, size / 2);
        var radius = (size - Thickness) / 2;

        dc.DrawEllipse(null, new Pen(Track, Thickness), center, radius, radius);

        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0)
            return;

        var pen = new Pen(ArcDrawing.Absolute(Stroke, size), Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        ArcDrawing.Draw(dc, pen, center, radius, 0, progress * 360);
    }
}

public sealed record DonutSegment(double Fraction, Brush Brush);

/// <summary>Rosca com segmentos proporcionais e gradiente.</summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<DonutSegment>), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(DonutChart),
        new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(DonutChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<DonutSegment>? Segments
    {
        get => (IReadOnlyList<DonutSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness)
            return;

        var center = new Point(size / 2, size / 2);
        var radius = (size - Thickness) / 2;
        dc.DrawEllipse(null, new Pen(Track, Thickness), center, radius, radius);

        var segments = Segments;
        if (segments is null || segments.Count == 0)
            return;

        var gap = segments.Count > 1 ? 3.0 : 0.0;
        var start = 0.0;
        foreach (var segment in segments)
        {
            var sweep = segment.Fraction * 360;
            var drawn = sweep - gap;
            if (drawn > 0.5)
            {
                var pen = new Pen(ArcDrawing.Absolute(segment.Brush, size), Thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                ArcDrawing.Draw(dc, pen, center, radius, start + gap / 2, drawn);
            }

            start += sweep;
        }
    }
}
