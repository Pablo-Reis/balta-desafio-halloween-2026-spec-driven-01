using System.Diagnostics;
using Productivity.Setup.Core;

namespace Productivity.Installer;

/// <summary>Registro de falhas do instalador (sem dados do usuário).</summary>
internal static class Logging
{
    public static string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProductivityMonitor-Setup.log");

    public static void Write(Exception exception)
    {
        try
        {
            File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERR] Falha no instalador: {exception}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Sem como gravar o log: a mensagem amigável já foi exibida.
        }
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] argv)
    {
        var args = SetupArguments.Parse(argv);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // A segunda etapa (após pedir administrador) abre enquanto a primeira janela ainda fecha: espera o mutex.
        using var mutex = new Mutex(false, "ProductivityMonitor.Setup" + (Branding.IsSandbox ? ".Sandbox" : string.Empty));
        bool owned;
        try
        {
            owned = mutex.WaitOne(TimeSpan.FromSeconds(args.Resume ? 10 : 0));
        }
        catch (AbandonedMutexException)
        {
            owned = true; // a instância anterior terminou sem liberar; a posse passa para esta
        }

        if (!owned)
        {
            if (!args.Silent)
                MessageBox.Show("O instalador já está aberto.", Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        try
        {
            return Run(args);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static int Run(SetupArguments args)
    {
        long payloadBytes;
        try
        {
            using var payload = InstallEngine.OpenPayload()
                ?? throw new InvalidOperationException("Este instalador não contém o pacote do programa. Gere-o com installer\\package.ps1.");
            payloadBytes = PayloadExtractor.TotalSize(payload);
        }
        catch (Exception ex)
        {
            Logging.Write(ex);
            if (!args.Silent)
                MessageBox.Show(ex.Message, Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        var installed = InstallEngine.FindInstalled();
        var kind = InstallPlanner.Determine(installed?.Version, Branding.Version);
        var defaultDirectory = args.Directory
            ?? installed?.Directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Branding.ProductName);

        if (args.Silent)
            return RunSilent(args, defaultDirectory);

        Application.Run(new SetupForm(args, installed, kind, defaultDirectory, payloadBytes));
        return 0;
    }
    /// <summary>Instalação sem janelas (/silent): usada em automações e testes. Códigos: 0 ok, 1 falha, 5 sem permissão.</summary>
    private static int RunSilent(SetupArguments args, string directory)
    {
        var error = InstallPathRules.Validate(directory);
        if (error is not null)
            return 1;

        directory = Path.GetFullPath(directory);
        if (Elevation.NeedsElevation(directory))
            return 5;

        try
        {
            new InstallEngine()
                .InstallAsync(new InstallRequest(directory, DesktopShortcut: !args.NoDesktop), new Progress<InstallProgress>(_ => { }), CancellationToken.None)
                .GetAwaiter().GetResult();

            if (!args.NoLaunch)
                Elevation.LaunchApp(Path.Combine(directory, Branding.ExeName));
            return 0;
        }
        catch (Exception ex)
        {
            Logging.Write(ex);
            return 1;
        }
    }
}

