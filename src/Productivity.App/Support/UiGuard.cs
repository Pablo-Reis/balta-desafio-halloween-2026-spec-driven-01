using System.Windows;
using Microsoft.Extensions.Logging;
using Productivity.Domain;

namespace Productivity.App.Support;

/// <summary>Tratamento central de erros: mensagem amigável ao usuário e log em arquivo (constituição).</summary>
public static class UiGuard
{
    public static ILogger? Logger { get; set; }

    public static void Handle(Exception exception, Window? owner = null)
    {
        if (exception is DomainException)
        {
            Show(exception.Message, MessageBoxImage.Warning, owner);
            return;
        }

        // Nunca registra textos digitados pelo usuário: apenas tipo, mensagem técnica e pilha.
        Logger?.LogError(exception, "Falha inesperada na aplicação.");
        Show("Ocorreu um erro inesperado. Tente novamente; se persistir, consulte o arquivo de log em %LOCALAPPDATA%\\ProductivityMonitor\\logs.", MessageBoxImage.Error, owner);
    }

    /// <summary>Executa uma ação assíncrona a partir de um evento de UI tratando qualquer falha.</summary>
    public static async void Run(Func<Task> action, Window? owner = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Handle(ex, owner);
        }
    }

    private static void Show(string message, MessageBoxImage image, Window? owner)
    {
        if (owner is { IsVisible: true })
            MessageBox.Show(owner, message, "Monitor de produtividade", MessageBoxButton.OK, image);
        else
            MessageBox.Show(message, "Monitor de produtividade", MessageBoxButton.OK, image);
    }
}
