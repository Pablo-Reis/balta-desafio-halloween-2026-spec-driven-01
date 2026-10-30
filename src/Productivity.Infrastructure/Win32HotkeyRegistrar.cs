using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Productivity.Application.Abstractions;
using Productivity.Domain;

namespace Productivity.Infrastructure;

/// <summary>Atalhos globais via RegisterHotKey, recebidos por uma janela invisível (message-only).</summary>
public sealed class Win32HotkeyRegistrar : NativeWindow, IHotkeyRegistrar
{
    private const int WmHotkey = 0x0312;
    private const int ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly ILogger<Win32HotkeyRegistrar> _logger;
    private readonly Dictionary<int, HotkeyAction> _registered = new();

    public Win32HotkeyRegistrar(ILogger<Win32HotkeyRegistrar> logger)
    {
        _logger = logger;
        CreateHandle(new CreateParams { Parent = HwndMessage });
    }

    public event Action<HotkeyAction>? Pressed;

    public IReadOnlyList<string> Apply(IReadOnlyDictionary<HotkeyAction, Hotkey> hotkeys)
    {
        UnregisterAll();
        var failures = new List<string>();
        var id = 1;

        foreach (var (action, hotkey) in hotkeys)
        {
            var modifiers = (int)hotkey.Modifiers | ModNoRepeat;
            if (RegisterHotKey(Handle, id, modifiers, hotkey.VirtualKey))
            {
                _registered[id] = action;
            }
            else
            {
                _logger.LogWarning("Não foi possível registrar o atalho da ação {Action} (erro {Error}).", action, Marshal.GetLastWin32Error());
                failures.Add($"O atalho {hotkey} já está em uso por outro programa. Escolha outro nas configurações.");
            }

            id++;
        }

        return failures;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && _registered.TryGetValue(m.WParam.ToInt32(), out var action))
            Pressed?.Invoke(action);

        base.WndProc(ref m);
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys)
            UnregisterHotKey(Handle, id);
        _registered.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
