using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Productivity.App.Views;
using Productivity.Domain;

namespace Productivity.App.Windows;

public partial class MainWindow : Window
{
    private const double CornerRadius = 28;
    private const double ShadowMargin = 14;
    private const double MaximizedMargin = 8;

    private readonly AppController _controller;
    private readonly UserControl[] _views;

    public MainWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        _views = new UserControl[]
        {
            new DashboardView(controller),
            new DemandsView(controller),
            new SettingsView(controller)
        };

        Glass.SizeChanged += (_, _) => ApplyGlassClip();
        UpdateShortcuts();
        Nav.SelectedIndex = 0;
    }

    private static string Display(Hotkey hotkey) => hotkey.ToString().Replace('+', ' ');

    private void UpdateShortcuts()
    {
        var s = _controller.Settings;
        KeyStart.Text = Display(s.StartHotkey);
        KeyPause.Text = Display(s.PauseHotkey);
        KeyFinish.Text = Display(s.FinishHotkey);
        KeyNote.Text = Display(s.NoteHotkey);
    }

    // Reflete atalhos alterados nas configurações assim que a janela volta ao foco.
    private void OnActivated(object? sender, EventArgs e) => UpdateShortcuts();

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedIndex >= 0)
            Host.Content = _views[Nav.SelectedIndex];
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // Minimizar ou fechar mantém o programa na bandeja (RF-01). "Sair" fica no menu da bandeja.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (Root is null)
            return;

        var maximized = WindowState == WindowState.Maximized;
        Root.Margin = new Thickness(maximized ? MaximizedMargin : ShadowMargin);
        var radius = maximized ? 0 : CornerRadius;
        Backdrop.CornerRadius = new CornerRadius(radius);
        GlassTint.CornerRadius = new CornerRadius(radius);
        Backdrop.Effect = maximized ? null : Backdrop.Effect ?? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 40, ShadowDepth = 14, Direction = 270, Opacity = 0.5 };
        ApplyGlassClip();

        if (WindowState == WindowState.Minimized)
            Hide();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_controller.IsExiting)
            return;

        e.Cancel = true;
        Hide();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyGlassClip();

    /// <summary>Recorta as manchas de cor nos cantos arredondados do vidro.</summary>
    private void ApplyGlassClip()
    {
        if (Glass.ActualWidth <= 0 || Glass.ActualHeight <= 0)
            return;

        var radius = WindowState == WindowState.Maximized ? 0 : CornerRadius;
        Glass.Clip = new RectangleGeometry(new Rect(0, 0, Glass.ActualWidth, Glass.ActualHeight), radius, radius);
    }
}


