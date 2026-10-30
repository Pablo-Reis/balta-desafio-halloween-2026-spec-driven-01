using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Productivity.Application.Persistence;
using Productivity.Domain;

namespace Productivity.Application.Services;

public interface ISettingsService
{
    event Action? SettingsChanged;

    Task<UserSettings> GetAsync();

    Task SaveAsync(UserSettings settings);
}

public sealed class SettingsService : ISettingsService
{
    private const string StartWithWindowsKey = "StartWithWindows";
    private const string ThemeKey = "Theme";
    private const string StartHotkeyKey = "StartHotkey";
    private const string PauseHotkeyKey = "PauseHotkey";
    private const string FinishHotkeyKey = "FinishHotkey";
    private const string NoteHotkeyKey = "NoteHotkey";
    private const string BreakAlertKey = "BreakAlertMinutes";
    private const string BreakDurationKey = "BreakDurationMinutes";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(IDbContextFactory<AppDbContext> factory, ILogger<SettingsService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public event Action? SettingsChanged;

    public async Task<UserSettings> GetAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var values = await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
        var d = UserSettings.Default;

        return new UserSettings(
            StartWithWindows: values.TryGetValue(StartWithWindowsKey, out var sw) && bool.TryParse(sw, out var swb) ? swb : d.StartWithWindows,
            Theme: values.TryGetValue(ThemeKey, out var th) && Enum.TryParse<AppTheme>(th, out var tm) ? tm : d.Theme,
            StartHotkey: ReadHotkey(values, StartHotkeyKey, d.StartHotkey),
            PauseHotkey: ReadHotkey(values, PauseHotkeyKey, d.PauseHotkey),
            FinishHotkey: ReadHotkey(values, FinishHotkeyKey, d.FinishHotkey),
            NoteHotkey: ReadHotkey(values, NoteHotkeyKey, d.NoteHotkey),
            BreakAlertMinutes: ReadMinutes(values, BreakAlertKey, d.BreakAlertMinutes),
            BreakDurationMinutes: ReadMinutes(values, BreakDurationKey, d.BreakDurationMinutes));
    }

    public async Task SaveAsync(UserSettings settings)
    {
        Validate(settings);

        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Settings.ToDictionaryAsync(s => s.Key);

        void Upsert(string key, string value)
        {
            if (existing.TryGetValue(key, out var setting))
                setting.ChangeValue(value);
            else
                db.Settings.Add(AppSetting.Create(key, value));
        }

        Upsert(StartWithWindowsKey, settings.StartWithWindows.ToString());
        Upsert(ThemeKey, settings.Theme.ToString());
        Upsert(StartHotkeyKey, settings.StartHotkey.ToString());
        Upsert(PauseHotkeyKey, settings.PauseHotkey.ToString());
        Upsert(FinishHotkeyKey, settings.FinishHotkey.ToString());
        Upsert(NoteHotkeyKey, settings.NoteHotkey.ToString());
        Upsert(BreakAlertKey, settings.BreakAlertMinutes.ToString());
        Upsert(BreakDurationKey, settings.BreakDurationMinutes.ToString());

        await db.SaveChangesAsync();
        _logger.LogInformation("Configurações salvas.");
        SettingsChanged?.Invoke();
    }

    private static void Validate(UserSettings s)
    {
        var hotkeys = new[] { s.StartHotkey, s.PauseHotkey, s.FinishHotkey, s.NoteHotkey };
        if (hotkeys.Distinct().Count() != hotkeys.Length)
            throw new DomainException("Cada ação precisa ter um atalho diferente. Há atalhos repetidos.");

        if (s.BreakAlertMinutes is < 0 or > UserSettings.MaxMinutes || s.BreakDurationMinutes is < 0 or > UserSettings.MaxMinutes)
            throw new DomainException($"Os tempos de alerta e de descanso devem estar entre 0 e {UserSettings.MaxMinutes} minutos (0 desativa o alerta).");
    }

    private static Hotkey ReadHotkey(Dictionary<string, string> values, string key, Hotkey fallback) =>
        values.TryGetValue(key, out var text) && Hotkey.TryParse(text, out var hotkey, out _) ? hotkey! : fallback;

    private static int ReadMinutes(Dictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var text) && int.TryParse(text, out var minutes) && minutes is >= 0 and <= UserSettings.MaxMinutes
            ? minutes
            : fallback;
}
