using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Productivity.App.Support;
using Productivity.App.Windows;
using Productivity.Application.Abstractions;
using Productivity.Application.Services;
using Productivity.Domain;
using Productivity.Infrastructure;

namespace Productivity.App;

/// <summary>Orquestra bandeja, atalhos, balão, notificações e janelas. Toda regra de negócio fica nas services.</summary>
public sealed class AppController
{
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly TrayIconService _tray;
    private readonly IAutoStartManager _autoStart;
    private readonly BreakMonitor _breakMonitor;
    private readonly ILogger<AppController> _logger;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private MainWindow? _main;
    private BubbleWindow? _bubble;
    private bool _promptOpen;

    public AppController(
        IDemandService demands,
        IDemandEventService events,
        INoteService notes,
        ISettingsService settingsService,
        IDashboardService dashboard,
        IClock clock,
        IHotkeyRegistrar hotkeys,
        TrayIconService tray,
        IAutoStartManager autoStart,
        BreakMonitor breakMonitor,
        ILogger<AppController> logger)
    {
        Demands = demands;
        Events = events;
        Notes = notes;
        SettingsService = settingsService;
        Dashboard = dashboard;
        Clock = clock;
        _hotkeys = hotkeys;
        _tray = tray;
        _autoStart = autoStart;
        _breakMonitor = breakMonitor;
        _logger = logger;
    }

    public IDemandService Demands { get; }
    public IDemandEventService Events { get; }
    public INoteService Notes { get; }
    public ISettingsService SettingsService { get; }
    public IDashboardService Dashboard { get; }
    public IClock Clock { get; }

    public UserSettings Settings { get; private set; } = UserSettings.Default;

    /// <summary>Demanda em andamento ou pausada (cache atualizado a cada mudança).</summary>
    public Demand? Active { get; private set; }

    public bool IsExiting { get; private set; }

    /// <summary>Disparado quando qualquer demanda muda.</summary>
    public event Action? DemandsChanged;

    /// <summary>Disparado a cada 500 ms para atualizar cronômetros.</summary>
    public event Action? Tick;

    public async Task StartAsync(bool minimized)
    {
        Settings = await SettingsService.GetAsync();
        ApplyTheme();
        SyncAutoStart();

        _tray.OpenRequested += () => ShowMainWindow();
        _tray.StartRequested += () => UiGuard.Run(PromptStartAsync);
        _tray.PauseResumeRequested += () => UiGuard.Run(TogglePauseAsync);
        _tray.FinishRequested += () => UiGuard.Run(FinishAsync);
        _tray.ExitRequested += Exit;

        _hotkeys.Pressed += action => UiGuard.Run(() => HandleHotkeyAsync(action));
        var warnings = ApplyHotkeys();

        Demands.DemandsChanged += () => UiGuard.Run(RefreshActiveAsync);
        await RefreshActiveAsync();

        _main = new MainWindow(this);
        _bubble = new BubbleWindow(this);
        _bubble.Refresh();

        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        if (!minimized)
            ShowMainWindow();

        if (warnings.Count > 0)
            Notify("Atalho indisponível", string.Join(" ", warnings));

        _logger.LogInformation("Aplicação iniciada.");
    }

    public void ShowMainWindow()
    {
        if (_main is null)
            return;

        _main.Show();
        if (_main.WindowState == WindowState.Minimized)
            _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void Notify(string title, string message) => _tray.Notify(title, message);

    public async Task PromptStartAsync()
    {
        if (_promptOpen)
            return;

        _promptOpen = true;
        try
        {
            var types = await Demands.ListTypesAsync();
            var window = new StartDemandWindow(types, Active);
            if (window.ShowDialog() != true)
                return;

            var demand = await Demands.StartAsync(window.DemandTitle, window.DemandType, window.Details, finishCurrent: true);
            Notify("Demanda iniciada", $"{demand.Type}: {demand.Title}");
        }
        finally
        {
            _promptOpen = false;
        }
    }

    public async Task TogglePauseAsync()
    {
        if (Active is null)
        {
            Notify("Nenhuma demanda em andamento", $"Use {Settings.StartHotkey} para iniciar uma demanda.");
            return;
        }

        var demand = await Demands.TogglePauseAsync();
        if (demand.Status == DemandStatus.Paused)
            Notify("Demanda pausada", $"Bom descanso! Use {Settings.PauseHotkey} para retomar.");
        else
            Notify("Demanda retomada", demand.Title);
    }

    public async Task FinishAsync()
    {
        if (Active is null)
        {
            Notify("Nenhuma demanda em andamento", $"Use {Settings.StartHotkey} para iniciar uma demanda.");
            return;
        }

        var demand = await Demands.FinishAsync();
        Notify("Demanda finalizada", $"{demand.Title} - {Format.Short(demand.GetWorkedTime(Clock.UtcNow))} trabalhados.");
    }

    public async Task PromptNoteAsync()
    {
        if (_promptOpen)
            return;

        if (Active is null)
        {
            Notify("Nenhuma demanda em andamento", "Inicie uma demanda antes de criar uma anotação.");
            return;
        }

        _promptOpen = true;
        try
        {
            var window = new NoteSpotlightWindow(Active.Title);
            if (window.ShowDialog() != true)
                return;

            await Notes.AddToActiveAsync(window.NoteText, NoteSource.Hotkey);
            Notify("Anotação salva", Active?.Title ?? string.Empty);
        }
        finally
        {
            _promptOpen = false;
        }
    }

    /// <summary>Salva e aplica as configurações. Retorna avisos (ex.: atalho já em uso).</summary>
    public async Task<IReadOnlyList<string>> SaveSettingsAsync(UserSettings settings)
    {
        await SettingsService.SaveAsync(settings);
        Settings = settings;
        ApplyTheme();
        SyncAutoStart();
        return ApplyHotkeys();
    }

    public void Exit()
    {
        IsExiting = true;
        _timer.Stop();
        _bubble?.Close();
        _main?.Close();
        _hotkeys.Dispose();
        _tray.Dispose();
        _logger.LogInformation("Aplicação encerrada.");
        System.Windows.Application.Current.Shutdown();
    }

    private async Task HandleHotkeyAsync(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.Start:
                await PromptStartAsync();
                break;
            case HotkeyAction.PauseResume:
                await TogglePauseAsync();
                break;
            case HotkeyAction.Finish:
                await FinishAsync();
                break;
            case HotkeyAction.Note:
                await PromptNoteAsync();
                break;
        }
    }

    private async Task RefreshActiveAsync()
    {
        Active = await Demands.GetActiveAsync();
        _tray.UpdateState(Active?.Status);
        _bubble?.Refresh();
        DemandsChanged?.Invoke();
    }

    private void OnTick()
    {
        _bubble?.Refresh();
        Tick?.Invoke();

        switch (_breakMonitor.Evaluate(Active, Settings))
        {
            case BreakAlert.TakeBreak:
                Notify("Hora de descansar", $"Você está trabalhando há {Settings.BreakAlertMinutes} min sem pausa. A demanda continua em andamento; pause quando quiser ({Settings.PauseHotkey}).");
                break;
            case BreakAlert.ReturnToWork:
                Notify("Hora de voltar", $"Seu descanso de {Settings.BreakDurationMinutes} min terminou. A demanda continua pausada; retome com {Settings.PauseHotkey}.");
                break;
        }
    }

    private void ApplyTheme()
    {
        AuroraTheme.Apply(Settings.Theme);
        System.Windows.Application.Current.ThemeMode = Settings.Theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }

    private void SyncAutoStart()
    {
        try
        {
            // Reescreve quando ativo para manter o caminho do executável atualizado.
            if (Settings.StartWithWindows || _autoStart.IsEnabled)
                _autoStart.SetEnabled(Settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao configurar o início com o Windows.");
            Notify("Início com o Windows", "Não foi possível alterar a configuração de início com o Windows.");
        }
    }

    private IReadOnlyList<string> ApplyHotkeys() => _hotkeys.Apply(new Dictionary<HotkeyAction, Hotkey>
    {
        [HotkeyAction.Start] = Settings.StartHotkey,
        [HotkeyAction.PauseResume] = Settings.PauseHotkey,
        [HotkeyAction.Finish] = Settings.FinishHotkey,
        [HotkeyAction.Note] = Settings.NoteHotkey
    });
}

