using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Productivity.App.Controls;

/// <summary>Ponto de status; só pulsa (halo expansivo) enquanto <see cref="IsLive"/> for verdadeiro.</summary>
public partial class StatusDot : UserControl
{
    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush), typeof(Brush), typeof(StatusDot),
        new PropertyMetadata(Brushes.Gray, (d, e) => ((StatusDot)d).ApplyBrush()));

    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(StatusDot),
        new PropertyMetadata(false, (d, _) => ((StatusDot)d).ApplyLive()));

    public StatusDot()
    {
        InitializeComponent();
        ApplyBrush();
        Loaded += (_, _) => ApplyLive();
        Unloaded += (_, _) => StopPulse();
    }

    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    public bool IsLive
    {
        get => (bool)GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    private void ApplyBrush()
    {
        Core.Fill = DotBrush;
        Halo.Fill = DotBrush;
    }

    private void ApplyLive()
    {
        if (IsLive && IsLoaded)
            StartPulse();
        else
            StopPulse();
    }

    private void StartPulse()
    {
        var duration = TimeSpan.FromSeconds(1.8);

        var scale = new DoubleAnimationUsingKeyFrames { Duration = duration, RepeatBehavior = RepeatBehavior.Forever };
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(3.2, KeyTime.FromPercent(0.7), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(3.2, KeyTime.FromPercent(1)));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration, RepeatBehavior = RepeatBehavior.Forever };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.7), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        Timeline.SetDesiredFrameRate(scale, 30);
        Timeline.SetDesiredFrameRate(fade, 30);
        HaloScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        HaloScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        Halo.BeginAnimation(OpacityProperty, fade);
    }

    private void StopPulse()
    {
        HaloScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        HaloScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Halo.BeginAnimation(OpacityProperty, null);
        Halo.Opacity = 0;
    }
}
