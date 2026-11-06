namespace Productivity.Infrastructure;

public static class AppPaths
{
    public const string AppName = "ProductivityMonitor";

    /// <summary>
    /// Variável de ambiente que isola uma execução (testes e demonstrações): usa outra pasta de dados,
    /// outra instância única e não altera o início automático do Windows.
    /// </summary>
    public const string SandboxVariable = "PRODUCTIVITYMONITOR_HOME";

    private static readonly string? SandboxDirectory = Environment.GetEnvironmentVariable(SandboxVariable);

    public static bool IsSandbox => !string.IsNullOrWhiteSpace(SandboxDirectory);

    public static string DataDirectory { get; } = IsSandbox
        ? Path.GetFullPath(SandboxDirectory!)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static string DatabasePath => Path.Combine(DataDirectory, "data.db");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
