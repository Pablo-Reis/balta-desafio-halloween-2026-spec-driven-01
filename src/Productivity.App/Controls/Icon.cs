using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Productivity.App.Controls;

/// <summary>Ícone de linha desenhado em grade 24x24, na cor do texto herdado (Foreground).</summary>
public sealed class Icon : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(Icon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled), typeof(bool), typeof(Icon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth), typeof(double), typeof(Icon),
        new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    static Icon()
    {
        FocusableProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        WidthProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(18.0));
        HeightProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(18.0));
    }

    public Icon()
    {
        IsHitTestVisible = false;
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ForegroundProperty)
            InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Data is null || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var scale = Math.Min(ActualWidth, ActualHeight) / 24.0;
        dc.PushTransform(new ScaleTransform(scale, scale));

        if (Filled)
        {
            dc.DrawGeometry(Foreground, null, Data);
        }
        else
        {
            var pen = new Pen(Foreground, StrokeWidth)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            dc.DrawGeometry(null, pen, Data);
        }

        dc.Pop();
    }
}

