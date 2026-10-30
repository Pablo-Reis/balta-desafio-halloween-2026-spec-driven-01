using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Productivity.App.Support;

/// <summary>Animações de apresentação reutilizáveis por propriedades anexadas.</summary>
public static class Motion
{
    private static readonly DependencyProperty GroupProperty =
        DependencyProperty.RegisterAttached("Group", typeof(TransformGroup), typeof(Motion));

    /// <summary>Cartão sobe e aparece ao carregar; o valor é o atraso em segundos (entrada escalonada).</summary>
    public static readonly DependencyProperty RiseProperty = DependencyProperty.RegisterAttached(
        "Rise", typeof(double?), typeof(Motion), new PropertyMetadata(null, OnRiseChanged));

    /// <summary>Ao passar o mouse o elemento sobe 2px e a borda clareia (apenas em Border).</summary>
    public static readonly DependencyProperty LiftProperty = DependencyProperty.RegisterAttached(
        "Lift", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnLiftChanged));

    /// <summary>Barra cresce de baixo para cima ao carregar; o valor é o atraso em segundos.</summary>
    public static readonly DependencyProperty GrowProperty = DependencyProperty.RegisterAttached(
        "Grow", typeof(double?), typeof(Motion), new PropertyMetadata(null, OnGrowChanged));

    public static double? GetRise(DependencyObject d) => (double?)d.GetValue(RiseProperty);

    public static void SetRise(DependencyObject d, double? value) => d.SetValue(RiseProperty, value);

    public static bool GetLift(DependencyObject d) => (bool)d.GetValue(LiftProperty);

    public static void SetLift(DependencyObject d, bool value) => d.SetValue(LiftProperty, value);

    public static double? GetGrow(DependencyObject d) => (double?)d.GetValue(GrowProperty);

    public static void SetGrow(DependencyObject d, double? value) => d.SetValue(GrowProperty, value);

    private static (ScaleTransform Scale, TranslateTransform Enter, TranslateTransform Hover) Transforms(FrameworkElement element)
    {
        if (element.GetValue(GroupProperty) is not TransformGroup group)
        {
            group = new TransformGroup();
            group.Children.Add(new ScaleTransform(1, 1));
            group.Children.Add(new TranslateTransform());
            group.Children.Add(new TranslateTransform());
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = group;
            element.SetValue(GroupProperty, group);
        }

        return ((ScaleTransform)group.Children[0], (TranslateTransform)group.Children[1], (TranslateTransform)group.Children[2]);
    }

    private static void OnRiseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not double delay)
            return;

        Transforms(element);
        element.Opacity = 0;
        element.Loaded += (_, _) => PlayRise(element, delay);
    }

    private static void PlayRise(FrameworkElement element, double delay)
    {
        var (scale, enter, _) = Transforms(element);
        var begin = TimeSpan.FromSeconds(delay);
        var duration = TimeSpan.FromSeconds(0.7);
        var ease = CubicBezierEase.Rise;

        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { BeginTime = begin, EasingFunction = ease });
        enter.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, duration) { BeginTime = begin, EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.985, 1, duration) { BeginTime = begin, EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.985, 1, duration) { BeginTime = begin, EasingFunction = ease });
    }

    private static void OnLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true)
            return;

        var (_, _, hover) = Transforms(element);

        element.MouseEnter += (_, _) =>
        {
            hover.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-2, TimeSpan.FromSeconds(0.35)) { EasingFunction = CubicBezierEase.Rise });
            if (element is Border border)
            {
                border.SetResourceReference(Border.BackgroundProperty, "Aurora.GlassHover");
                border.SetResourceReference(Border.BorderBrushProperty, "Aurora.GlassBorderHover");
            }
        };

        element.MouseLeave += (_, _) =>
        {
            hover.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, TimeSpan.FromSeconds(0.35)) { EasingFunction = CubicBezierEase.Rise });
            if (element is Border border)
            {
                border.ClearValue(Border.BackgroundProperty);
                border.ClearValue(Border.BorderBrushProperty);
            }
        };
    }

    private static void OnGrowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not double delay)
            return;

        var scale = new ScaleTransform(1, 0);
        element.RenderTransformOrigin = new Point(0.5, 1);
        element.RenderTransform = scale;
        element.Loaded += (_, _) => scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.9))
            {
                BeginTime = TimeSpan.FromSeconds(delay),
                EasingFunction = CubicBezierEase.Rise
            });
    }
}
