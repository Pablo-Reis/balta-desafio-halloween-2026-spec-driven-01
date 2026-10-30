using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Productivity.Application.Abstractions;

namespace Productivity.Infrastructure;

/// <summary>Início com o Windows via HKCU\...\Run (não exige administrador).</summary>
public sealed class RegistryAutoStartManager : IAutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly ILogger<RegistryAutoStartManager> _logger;

    public RegistryAutoStartManager(ILogger<RegistryAutoStartManager> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppPaths.AppName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);

        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável indisponível.");
            key.SetValue(AppPaths.AppName, $"\"{path}\" --minimized");
            _logger.LogInformation("Início com o Windows ativado.");
        }
        else
        {
            key.DeleteValue(AppPaths.AppName, throwOnMissingValue: false);
            _logger.LogInformation("Início com o Windows desativado.");
        }
    }
}
