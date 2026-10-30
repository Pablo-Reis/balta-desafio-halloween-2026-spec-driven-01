using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Productivity.App.Controls;
using Productivity.App.Support;
using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.App.Views;

public partial class DashboardView : UserControl
{
    private const double ChartHeight = 120;
    private const double EmptyBarHeight = 8;
    private const int RefreshEveryTicks = 30;

    private static readonly CultureInfo Culture = new("pt-BR");
    private static readonly DropShadowEffect BarGlow = CreateGlow();

    private readonly AppController _controller;
    private int _ticks;
    private bool _chartAnimated;

    public DashboardView(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
    }

    private sealed record DayBar(
        string Label,
        Brush LabelBrush,
        double BarWidth,
        double BarHeight,
        Brush Fill,
        Effect? Glow,
        double? Delay,
        Visibility ChipVisibility,
        string ChipText,
        string Tooltip);

    private sealed record TypeRow(string Type, double Share, string Summary, Brush Swatch);

    private static DropShadowEffect CreateGlow()
    {
        var glow = new DropShadowEffect
        {
            Color = (Color)ColorConverter.ConvertFromString("#8B5CF6"),
            BlurRadius = 22,
            ShadowDepth = 0,
            Opacity = 0.45
        };
        glow.Freeze();
        return glow;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _controller.DemandsChanged += OnDemandsChanged;
        _controller.Tick += OnTick;
        UpdateHeader();
        UpdateCurrent();
        UiGuard.Run(LoadDashboardAsync);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _controller.DemandsChanged -= OnDemandsChanged;
        _controller.Tick -= OnTick;
    }

    private void OnTick()
    {
        UpdateCurrent();

        // Mantém Hoje/Total/gráfico atualizados enquanto a demanda corre (a cada ~15 s).
        if (++_ticks % RefreshEveryTicks == 0)
        {
            UpdateHeader();
            if (_controller.Active is not null)
                UiGuard.Run(LoadDashboardAsync);
        }
    }

    private void OnDemandsChanged()
    {
        UpdateCurrent();
        UiGuard.Run(LoadDashboardAsync);
    }

    private void UpdateHeader()
    {
        var now = DateTime.Now;
        var date = now.ToString("dddd, d 'de' MMMM", Culture);
        DateText.Text = char.ToUpper(date[0], Culture) + date[1..];

        var greeting = now.Hour switch
        {
            < 12 => "Bom dia",
            < 18 => "Boa tarde",
            _ => "Boa noite"
        };
        var name = Culture.TextInfo.ToTitleCase(Environment.UserName.ToLower(Culture));
        GreetingText.Text = $"{greeting}, {name}";
    }

    private void UpdateCurrent()
    {
        var demand = _controller.Active;
        var now = _controller.Clock.UtcNow;

        if (demand is null)
        {
            StatusDot.DotBrush = AuroraBrushes.Resource("Aurora.TextMuted");
            StatusDot.IsLive = false;
            StatusBadge.Background = AuroraBrushes.Resource("Aurora.Chip");
            StatusBadge.BorderBrush = AuroraBrushes.Resource("Aurora.ChipBorder");
            StatusText.Foreground = AuroraBrushes.Resource("Aurora.TextSecondary");
            StatusText.Text = "Sem sessão ativa";
            CurrentType.Text = string.Empty;
            CurrentTitle.Text = "Pronto para começar";
            CurrentDescription.Text = $"Use {_controller.Settings.StartHotkey} ou o botão para iniciar uma demanda.";
            CurrentDescription.Visibility = Visibility.Visible;
            CurrentTimer.Text = "--:--:--";
            CurrentExtra.Text = string.Empty;
            Ring.Progress = 0;
            StartButton.Visibility = Visibility.Visible;
            PauseButton.Visibility = Visibility.Collapsed;
            FinishButton.Visibility = Visibility.Collapsed;
            return;
        }

        var working = demand.Status == DemandStatus.Running;
        var shown = working ? demand.GetWorkedTime(now) : demand.GetTimeSinceLastEvent(now);

        StatusDot.DotBrush = AuroraBrushes.Resource(working ? "RunningBrush" : "PausedBrush");
        StatusDot.IsLive = working;
        StatusBadge.Background = AuroraBrushes.Resource(working ? "RunningBg" : "PausedBg");
        StatusBadge.BorderBrush = AuroraBrushes.Resource(working ? "RunningBorder" : "PausedBorder");
        StatusText.Foreground = AuroraBrushes.Resource(working ? "RunningBrush" : "PausedBrush");
        StatusText.Text = Format.StatusLabel(demand.Status);
        CurrentType.Text = demand.Type.ToUpperInvariant();
        CurrentTitle.Text = demand.Title;
        CurrentDescription.Text = demand.Description;
        CurrentDescription.Visibility = demand.Description.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        CurrentTimer.Text = Format.Duration(shown);
        CurrentExtra.Text = working
            ? $"Pausas: {Format.Short(demand.GetPausedTime(now))}"
            : $"Trabalhado: {Format.Duration(demand.GetWorkedTime(now))}";
        Ring.Progress = shown.TotalSeconds % 60 / 60;

        StartButton.Visibility = Visibility.Collapsed;
        PauseButton.Visibility = Visibility.Visible;
        FinishButton.Visibility = Visibility.Visible;
        PauseLabel.Text = working ? "Pausar" : "Retomar";
        PauseIcon.Data = (Geometry)FindResource(working ? "Icon.Pause" : "Icon.Play");
    }

    private async Task LoadDashboardAsync()
    {
        var data = await _controller.Dashboard.GetLast30DaysAsync();
        var max = data.Days.Max(d => d.Worked.TotalMinutes);
        var animate = !_chartAnimated;
        _chartAnimated = true;

        var muted = AuroraBrushes.Resource("Aurora.TextMuted");
        var white = AuroraBrushes.Resource("Aurora.Text");
        var empty = AuroraBrushes.Resource("Aurora.BarEmpty");
        var filled = AuroraBrushes.Resource("Aurora.AccentVertical");

        DaysChart.ItemsSource = data.Days.Select((d, i) =>
        {
            var hasValue = d.Worked > TimeSpan.Zero;
            var isToday = i == data.Days.Count - 1;
            return new DayBar(
                Label: d.Date.Day.ToString(),
                LabelBrush: isToday ? white : muted,
                BarWidth: 18,
                BarHeight: hasValue && max > 0 ? Math.Max(EmptyBarHeight, d.Worked.TotalMinutes / max * ChartHeight) : EmptyBarHeight,
                Fill: hasValue ? filled : empty,
                Glow: hasValue ? BarGlow : null,
                Delay: animate ? (isToday ? 0.5 : 0.3 + i * 0.012) : null,
                ChipVisibility: isToday && hasValue ? Visibility.Visible : Visibility.Collapsed,
                ChipText: Format.Short(d.Worked),
                Tooltip: $"{d.Date:dd/MM/yyyy}: {(hasValue ? Format.Short(d.Worked) : "sem registro")}");
        }).ToList();

        SetKpi(TodayText, data.Days[^1].Worked);
        SetKpi(TotalText, data.TotalWorked);
        SetKpi(AverageText, data.AveragePerDay);
        CountText.Text = data.DemandCount.ToString();

        var typeTotal = data.ByType.Sum(t => t.Worked.TotalMinutes);
        var rows = data.ByType.Select((t, i) =>
        {
            var share = typeTotal <= 0 ? 0 : t.Worked.TotalMinutes / typeTotal;
            return new TypeRow(t.Type, share, $"{Format.Short(t.Worked)} · {share:P0}", AuroraBrushes.TypeBrush(i));
        }).ToList();

        TypesList.ItemsSource = rows;
        Donut.Segments = rows.Select(r => new DonutSegment(r.Share, r.Swatch)).ToList();
        DonutCenter.Text = rows.Count == 0 ? string.Empty : rows[0].Share.ToString("P0");
        NoTypesText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TypesPanel.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetKpi(TextBlock target, TimeSpan span)
    {
        var (value, unit) = Format.ShortParts(span);
        target.Inlines.Clear();
        target.Inlines.Add(new Run(value));
        target.Inlines.Add(new Run($" {unit}")
        {
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = AuroraBrushes.Resource("Aurora.TextSecondary")
        });
    }

    private void OnStartClick(object sender, RoutedEventArgs e) => UiGuard.Run(_controller.PromptStartAsync, Window.GetWindow(this));

    private void OnPauseClick(object sender, RoutedEventArgs e) => UiGuard.Run(_controller.TogglePauseAsync, Window.GetWindow(this));

    private void OnFinishClick(object sender, RoutedEventArgs e) => UiGuard.Run(_controller.FinishAsync, Window.GetWindow(this));
}

