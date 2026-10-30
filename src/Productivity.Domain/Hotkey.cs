using System.Text.RegularExpressions;

namespace Productivity.Domain;

/// <summary>Valores iguais aos MOD_* do Windows.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

/// <summary>Atalho global, ex.: "Ctrl+Alt+S". Exige ao menos um modificador.</summary>
public sealed partial record Hotkey
{
    private Hotkey(HotkeyModifiers modifiers, string key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public HotkeyModifiers Modifiers { get; }

    /// <summary>Letra A-Z, dígito 0-9 ou F1-F12.</summary>
    public string Key { get; }

    /// <summary>Código de tecla virtual do Windows.</summary>
    public int VirtualKey => Key.Length == 1 ? Key[0] : 0x70 + int.Parse(Key[1..]) - 1;

    public static Hotkey Create(HotkeyModifiers modifiers, string key)
    {
        key = (key ?? string.Empty).Trim().ToUpperInvariant();

        if (modifiers == HotkeyModifiers.None)
            throw new DomainException("O atalho precisa ter ao menos uma tecla modificadora (Ctrl, Alt, Shift ou Win).");
        if (!KeyPattern().IsMatch(key))
            throw new DomainException("A tecla do atalho deve ser uma letra, um número ou F1 a F12.");

        return new Hotkey(modifiers, key);
    }

    public static Hotkey Parse(string text)
    {
        if (!TryParse(text, out var hotkey, out var error))
            throw new DomainException(error);
        return hotkey!;
    }

    public static bool TryParse(string? text, out Hotkey? hotkey, out string error)
    {
        hotkey = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "O atalho não pode ser vazio.";
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        string? key = null;

        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "WIN":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    key = part;
                    break;
            }
        }

        try
        {
            hotkey = Create(modifiers, key ?? string.Empty);
            return true;
        }
        catch (DomainException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }

    [GeneratedRegex("^([A-Z0-9]|F([1-9]|1[0-2]))$")]
    private static partial Regex KeyPattern();
}
