using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Productivity.App.Support;
using Productivity.Domain;

namespace Productivity.App.Windows;

/// <summary>
/// Balão sempre no topo: pílula 196x48 que se transforma (estilo Dynamic Island) em cartão 400x282 (RF-14/RF-15).
/// </summary>
public partial class BubbleWindow : Window
{
    private const double PillWidth = 220;
    private const double PillHeight = 48;
    private const double PillRadius = 24;
    private const double CardWidth = 400;
    private const double CardHeight = 282;
    private const double CardRadius = 30;
    private static readonly Duration TransitionTime = new(TimeSpan.FromSeconds(0.55));

    public static readonly DependencyProperty ExpansionProperty = DependencyProperty.Register(
        nameof(Expansion), typeof(double), typeof(BubbleWindow),
        new PropertyMetadata(0.0, (d, e) => ((BubbleWindow)d).ApplyExpansion((double)e.NewValue)));

    // Margem entre a ilha e a borda da janela transparente (espaço da sombra).
    private const double IslandMargin = 24;

    private readonly AppController _controller;
    private bool _expanded;
    private bool _userMoved;
    private Point? _pressPoint;

    public BubbleWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        ApplyExpansion(0);
        Reposition();
    }

    /// <summary>0 = pílula recolhida, 1 = cartão expandido.</summary>
    public double Expansion
    {
        get => (double)GetValue(ExpansionProperty);
        set => SetValue(ExpansionProperty, value);
    }

    public void Refresh()
    {
        var demand = _controller.Active;
        if (demand is null)
        {
            if (IsVisible)
            {
                Hide();
                SetExpanded(false, animate: false);
            }

            return;
        }

        var now = _controller.Clock.UtcNow;
        var working = demand.Status == DemandStatus.Running;
        var label = Format.StatusLabel(demand.Status);
        var brush = AuroraBrushes.Resource(working ? "RunningBrush" : "PausedBrush");
        // Trabalhando: tempo trabalhado acumulado. Descansando: tempo do descanso atual.
        var shown = working ? demand.GetWorkedTime(now) : demand.GetTimeSinceLastEvent(now);
        var timer = Format.Duration(shown);
        var seconds = shown.TotalSeconds % 60 / 60;

        PillDot.DotBrush = brush;
        PillDot.IsLive = working;
        PillStatus.Text = label;
        PillTime.Text = timer;
        PillRing.Progress = seconds;

        CardDot.DotBrush = brush;
        CardDot.IsLive = working;
        CardBadge.Background = AuroraBrushes.Resource(working ? "RunningBg" : "PausedBg");
        CardStatus.Foreground = brush;
        CardStatus.Text = label;
        CardTime.Text = timer;
        CardExtra.Text = working
            ? $"Pausas: {Format.Short(demand.GetPausedTime(now))}"
            : $"Total trabalhado: {Format.Duration(demand.GetWorkedTime(now))}";
        CardType.Text = demand.Type.ToUpperInvariant();
        CardTitle.Text = demand.Title;
        CardDescription.Text = demand.Description;
        CardDescription.Visibility = demand.Description.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        PauseLabel.Text = working ? "Pausar" : "Retomar";
        PauseIcon.Data = (Geometry)FindResource(working ? "Icon.Pause" : "Icon.Play");
        UpdateProgressLine(seconds);

        if (!IsVisible)
        {
            // Mantém a posição escolhida pelo usuário durante a sessão; só volta ao canto padrão ao reabrir o programa.
            if (!_userMoved)
                Reposition();
            Show();
        }
    }

    // Arrastar: clicar e mover a pílula (ou o corpo do cartão) leva o balão pela tela. Um clique simples continua expandindo.
    private void OnIslandMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = IsOnActionButton(e.OriginalSource as DependencyObject) ? null : e.GetPosition(this);
    }

    private void OnIslandMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            _pressPoint = null;
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pressPoint = null;
        _userMoved = true;
        Mouse.Capture(null);
        DragMove();
        KeepOnScreen();
    }

    private void OnIslandMouseUp(object sender, MouseButtonEventArgs e) => _pressPoint = null;

    /// <summary>Botões de ação (pausar, finalizar, recolher) não iniciam arrasto; a pílula sim.</summary>
    private bool IsOnActionButton(DependencyObject? source)
    {
        while (source is not null && !ReferenceEquals(source, Island))
        {
            if (source is System.Windows.Controls.Button button && !ReferenceEquals(button, PillLayer))
                return true;

            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    /// <summary>Impede que o cartão (inclusive expandido) fique fora da área visível das telas.</summary>
    private void KeepOnScreen()
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;

        var cardRight = Width - IslandMargin;
        var cardBottom = IslandMargin + CardHeight;

        Left = Math.Clamp(Left, left - (cardRight - CardWidth), right - cardRight);
        Top = Math.Clamp(Top, top - IslandMargin, bottom - cardBottom);
    }

    private void OnExpandClick(object sender, RoutedEventArgs e) => SetExpanded(true, animate: true);

    private void OnCollapseClick(object sender, RoutedEventArgs e) => SetExpanded(false, animate: true);

    private void OnPauseClick(object sender, RoutedEventArgs e) => UiGuard.Run(_controller.TogglePauseAsync, this);

    private void OnFinishClick(object sender, RoutedEventArgs e) => UiGuard.Run(_controller.FinishAsync, this);

    private void SetExpanded(bool expanded, bool animate)
    {
        _expanded = expanded;
        var target = expanded ? 1.0 : 0.0;

        if (animate)
        {
            BeginAnimation(ExpansionProperty, new DoubleAnimation(target, TransitionTime) { EasingFunction = CubicBezierEase.Island });
        }
        else
        {
            BeginAnimation(ExpansionProperty, null);
            Expansion = target;
        }
    }

    private double _lastSeconds;

    private void UpdateProgressLine(double seconds)
    {
        _lastSeconds = seconds;
        ProgressFill.Width = Math.Max(0, Island.Width - 2) * seconds;
    }

    /// <summary>Aplica largura, altura, raio e a troca de conteúdo (fade + blur) para o progresso t da animação.</summary>
    private void ApplyExpansion(double t)
    {
        Island.Width = PillWidth + (CardWidth - PillWidth) * t;
        Island.Height = PillHeight + (CardHeight - PillHeight) * t;
        var radius = PillRadius + (CardRadius - PillRadius) * t;
        Island.CornerRadius = new CornerRadius(radius);
        IslandContent.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, Island.Width - 2), Math.Max(0, Island.Height - 2)), radius - 1, radius - 1);

        var pillOpacity = Math.Clamp(1 - t / 0.45, 0, 1);
        var cardOpacity = Math.Clamp((t - 0.25) / 0.6, 0, 1);

        PillLayer.Opacity = pillOpacity;
        PillLayer.IsHitTestVisible = t < 0.5;
        PillLayer.Effect = Blur(6 * (1 - pillOpacity));

        CardLayer.Opacity = cardOpacity;
        CardLayer.IsHitTestVisible = t > 0.5;
        CardLayer.Effect = Blur(8 * (1 - cardOpacity));
        CardScale.ScaleX = CardScale.ScaleY = 0.92 + 0.08 * t;
        CardShift.Y = -12 * (1 - t);

        UpdateProgressLine(_lastSeconds);
    }

    private static BlurEffect? Blur(double radius) => radius < 0.2 ? null : new BlurEffect { Radius = radius };

    private void Reposition()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width + 8;
        Top = area.Top - 8;
    }
}



