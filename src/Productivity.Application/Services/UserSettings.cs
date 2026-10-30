using Productivity.Domain;

namespace Productivity.Application.Services;

public sealed record UserSettings(
    bool StartWithWindows,
    AppTheme Theme,
    Hotkey StartHotkey,
    Hotkey PauseHotkey,
    Hotkey FinishHotkey,
    Hotkey NoteHotkey,
    int BreakAlertMinutes,
    int BreakDurationMinutes)
{
    public const int MaxMinutes = 1440;

    public static UserSettings Default { get; } = new(
        StartWithWindows: false,
        Theme: AppTheme.System,
        StartHotkey: Hotkey.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt, "S"),
        PauseHotkey: Hotkey.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt, "P"),
        FinishHotkey: Hotkey.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt, "F"),
        NoteHotkey: Hotkey.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt, "N"),
        BreakAlertMinutes: 50,
        BreakDurationMinutes: 10);
}
