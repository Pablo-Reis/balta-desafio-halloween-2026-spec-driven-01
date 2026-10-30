using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Productivity.App.Support;
using Productivity.Application.Services;
using Productivity.Domain;
using Productivity.Infrastructure;

namespace Productivity.App.Views;

public partial class SettingsView : UserControl
{
    private readonly AppController _controller;
    private readonly Dictionary<TextBox, Hotkey> _hotkeys = new();

    public SettingsView(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Fill(_controller.Settings);
        LogPathText.Text = $"Dados em {AppPaths.DataDirectory} · logs em {AppPaths.LogsDirectory}";
    }

    private void Fill(UserSettings s)
    {
        AutoStartBox.IsChecked = s.StartWithWindows;
        ThemeSystem.IsChecked = s.Theme == AppTheme.System;
        ThemeLight.IsChecked = s.Theme == AppTheme.Light;
        ThemeDark.IsChecked = s.Theme == AppTheme.Dark;

        SetHotkey(StartHotkeyBox, s.StartHotkey);
        SetHotkey(PauseHotkeyBox, s.PauseHotkey);
        SetHotkey(FinishHotkeyBox, s.FinishHotkey);
        SetHotkey(NoteHotkeyBox, s.NoteHotkey);

        BreakAlertBox.Text = s.BreakAlertMinutes.ToString();
        BreakDurationBox.Text = s.BreakDurationMinutes.ToString();
    }

    private void SetHotkey(TextBox box, Hotkey hotkey)
    {
        _hotkeys[box] = hotkey;
        box.Text = hotkey.ToString();
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        e.Handled = true;

        var hotkey = HotkeyCapture.FromKeyEvent(e);
        if (hotkey is not null)
            SetHotkey(box, hotkey);
    }

    private void OnNumberInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsDigit);

    private void OnDefaultsClick(object sender, RoutedEventArgs e)
    {
        Fill(UserSettings.Default);
        Show("Valores padrão carregados. Clique em \"Salvar configurações\" para aplicar.", false);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => UiGuard.Run(async () =>
    {
        var settings = new UserSettings(
            StartWithWindows: AutoStartBox.IsChecked == true,
            Theme: ThemeLight.IsChecked == true ? AppTheme.Light : ThemeDark.IsChecked == true ? AppTheme.Dark : AppTheme.System,
            StartHotkey: _hotkeys[StartHotkeyBox],
            PauseHotkey: _hotkeys[PauseHotkeyBox],
            FinishHotkey: _hotkeys[FinishHotkeyBox],
            NoteHotkey: _hotkeys[NoteHotkeyBox],
            BreakAlertMinutes: ParseMinutes(BreakAlertBox.Text),
            BreakDurationMinutes: ParseMinutes(BreakDurationBox.Text));

        var warnings = await _controller.SaveSettingsAsync(settings);
        Show(warnings.Count == 0 ? "Configurações salvas e aplicadas." : $"Configurações salvas, mas: {string.Join(" ", warnings)}", warnings.Count > 0);
    }, Window.GetWindow(this));

    private static int ParseMinutes(string text) =>
        int.TryParse(text, out var minutes) ? minutes : throw new DomainException("Informe os tempos de alerta e de descanso em minutos (somente números).");

    private void Show(string message, bool isWarning)
    {
        StatusText.Text = message;
        StatusText.Foreground = isWarning ? (Brush)FindResource("PausedBrush") : (Brush)FindResource("RunningBrush");
    }
}
