using System.Reflection;

namespace Productivity.Installer;

/// <summary>Identidade do produto e nomes usados no Windows (atalhos e lista de programas).</summary>
internal static class Branding
{
    public const string ProductName = "Monitor de produtividade";
    public const string ExeName = "ProductivityMonitor.exe";
    public const string UninstallScript = "uninstall.ps1";

    /// <summary>Execução isolada (testes): nomes diferentes e sem mexer na instalação real do usuário.</summary>
    public static bool IsSandbox => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRODUCTIVITYMONITOR_HOME"));

    public static string ShortcutName => IsSandbox ? $"{ProductName} (teste).lnk" : $"{ProductName}.lnk";

    public static string UninstallKeyName => IsSandbox ? "ProductivityMonitor.Sandbox" : "ProductivityMonitor";

    public static string Version
    {
        get
        {
            var text = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
            var cut = text.IndexOf('+');
            return cut >= 0 ? text[..cut] : text;
        }
    }
}
