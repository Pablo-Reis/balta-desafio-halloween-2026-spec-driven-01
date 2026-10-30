using Productivity.Domain;

namespace Productivity.Application.Abstractions;

public enum HotkeyAction
{
    Start,
    PauseResume,
    Finish,
    Note
}

public interface IHotkeyRegistrar : IDisposable
{
    event Action<HotkeyAction>? Pressed;

    /// <summary>Registra os atalhos da configuração. Retorna mensagens (pt-BR) dos atalhos que falharam.</summary>
    IReadOnlyList<string> Apply(IReadOnlyDictionary<HotkeyAction, Hotkey> hotkeys);
}

public interface INotificationSender
{
    void Notify(string title, string message);
}

public interface IAutoStartManager
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
