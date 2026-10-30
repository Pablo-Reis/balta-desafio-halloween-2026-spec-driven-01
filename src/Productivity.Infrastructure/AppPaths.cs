namespace Productivity.Infrastructure;

public static class AppPaths
{
    public const string AppName = "ProductivityMonitor";

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static string DatabasePath => Path.Combine(DataDirectory, "data.db");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
