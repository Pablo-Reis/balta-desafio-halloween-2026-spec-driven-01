using System.Windows;
using System.Windows.Input;
using Productivity.Domain;

namespace Productivity.App.Support;

public static class HotkeyCapture
{
    /// <summary>Converte a tecla pressionada em atalho; retorna null enquanto só há modificadores ou tecla inválida.</summary>
    public static Hotkey? FromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var name = KeyName(key);
        if (name is null)
            return null;

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;

        return Hotkey.TryParse($"{string.Join("+", ModifierNames(modifiers))}+{name}", out var hotkey, out _) ? hotkey : null;
    }

    private static IEnumerable<string> ModifierNames(HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) yield return "Alt";
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) yield return "Shift";
        if (modifiers.HasFlag(HotkeyModifiers.Win)) yield return "Win";
    }

    private static string? KeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return key.ToString();
        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return ((int)(key - Key.NumPad0)).ToString();
        if (key is >= Key.F1 and <= Key.F12)
            return key.ToString();
        return null;
    }
}
