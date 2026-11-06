using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using Productivity.Setup.Core;

namespace Productivity.Installer;

internal readonly record struct InstallProgress(double Fraction, string Status, string Detail);

internal sealed record InstallRequest(string Directory, bool DesktopShortcut);

/// <summary>Versão do programa já instalada (lida da lista de programas do Windows).</summary>
internal sealed record InstalledInfo(string Version, string Directory, bool MachineWide);

/// <summary>Copia o programa, cria atalhos e registra na lista de programas, com reversão em caso de falha.</summary>
internal sealed class InstallEngine
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    // ---- Pacote embutido --------------------------------------------------------------------------------------

    public static Stream? OpenPayload() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");

    // ---- Detecção de instalação existente ---------------------------------------------------------------------

    public static InstalledInfo? FindInstalled()
    {
        foreach (var (hive, machine) in new[] { (Registry.LocalMachine, true), (Registry.CurrentUser, false) })
        {
            using var key = hive.OpenSubKey($@"{UninstallRoot}\{Branding.UninstallKeyName}");
            if (key?.GetValue("InstallLocation") is string directory
                && Directory.Exists(directory)
                && File.Exists(Path.Combine(directory, Branding.ExeName)))
            {
                return new InstalledInfo(key.GetValue("DisplayVersion") as string ?? string.Empty, directory, machine);
            }
        }

        return null;
    }

    // ---- Instalação -------------------------------------------------------------------------------------------

    public async Task InstallAsync(InstallRequest request, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        var machineWide = Elevation.IsElevated;
        var target = Path.GetFullPath(request.Directory);
        var staging = target + ".setup-new";
        var backup = target + ".setup-old";
        var swapped = false;

        try
        {
            progress.Report(new InstallProgress(0.01, "Preparando a instalação…", string.Empty));
            DeleteQuietly(staging);
            DeleteQuietly(backup);

            progress.Report(new InstallProgress(0.03, "Encerrando o programa, se estiver aberto…", string.Empty));
            CloseRunningInstances(target);
            cancellationToken.ThrowIfCancellationRequested();

            await ExtractToStagingAsync(staging, progress, cancellationToken);

            progress.Report(new InstallProgress(0.94, "Finalizando a instalação…", string.Empty));
            cancellationToken.ThrowIfCancellationRequested();

            // A partir daqui a pasta antiga é trocada: qualquer falha restaura a versão anterior.
            if (Directory.Exists(target))
                Directory.Move(target, backup);
            try
            {
                Directory.Move(staging, target);
                swapped = true;
            }
            catch
            {
                if (Directory.Exists(backup) && !Directory.Exists(target))
                    Directory.Move(backup, target);
                throw;
            }

            progress.Report(new InstallProgress(0.97, "Criando atalhos…", string.Empty));
            CreateShortcuts(target, request.DesktopShortcut, machineWide);

            progress.Report(new InstallProgress(0.99, "Registrando o programa no Windows…", string.Empty));
            RegisterUninstall(target, machineWide);
            RemoveLegacyPerUserInstall(target);

            DeleteQuietly(backup);
            progress.Report(new InstallProgress(1, "Instalação concluída.", string.Empty));
        }
        catch
        {
            DeleteQuietly(staging);
            // Falha depois da troca: devolve a versão anterior (se havia) para não deixar o usuário sem o programa.
            if (swapped && Directory.Exists(backup))
            {
                DeleteQuietly(target);
                TryMove(backup, target);
            }

            throw;
        }
    }

    private static async Task ExtractToStagingAsync(string staging, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        await using var payload = OpenPayload()
            ?? throw new InvalidOperationException("Este instalador não contém o pacote do programa. Gere-o com installer\\package.ps1.");

        // Repassa na mesma ordem: o Progress recebido já envia ao thread da interface em fila.
        var extraction = new SyncProgress<ExtractProgress>(p =>
            progress.Report(new InstallProgress(
                0.05 + p.Fraction * 0.88,
                $"Copiando arquivos… {p.FilesDone} de {p.FilesTotal}",
                p.CurrentFile)));

        await Task.Run(() => PayloadExtractor.ExtractAsync(payload, staging, extraction, cancellationToken), cancellationToken);
    }
    /// <summary>Progress que repassa direto (o Progress padrão já envia ao thread da interface).</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public SyncProgress(Action<T> report) => _report = report;

        public void Report(T value) => _report(value);
    }

    // ---- Processos --------------------------------------------------------------------------------------------

    private static void CloseRunningInstances(string target)
    {
        var root = target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Branding.ExeName)))
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is not null && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception)
            {
                // Sem acesso ao processo (outro usuário/elevação): segue; se o arquivo estiver em uso, a troca da pasta avisa.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    // ---- Atalhos ----------------------------------------------------------------------------------------------

    private static void CreateShortcuts(string target, bool desktop, bool machineWide)
    {
        var exe = Path.Combine(target, Branding.ExeName);
        var startMenu = Environment.GetFolderPath(machineWide ? Environment.SpecialFolder.CommonPrograms : Environment.SpecialFolder.Programs);
        CreateShortcut(Path.Combine(startMenu, Branding.ShortcutName), exe, target);

        if (desktop)
        {
            var desktopFolder = Environment.GetFolderPath(machineWide ? Environment.SpecialFolder.CommonDesktopDirectory : Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktopFolder, Branding.ShortcutName), exe, target);
        }
    }

    private static void CreateShortcut(string path, string exe, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Não foi possível criar atalhos neste computador.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = exe;
        link.WorkingDirectory = workingDirectory;
        link.Description = Branding.ProductName;
        link.IconLocation = exe;
        link.Save();
    }

    // ---- Lista de programas do Windows ------------------------------------------------------------------------

    private static void RegisterUninstall(string target, bool machineWide)
    {
        var hive = machineWide ? Registry.LocalMachine : Registry.CurrentUser;
        using var key = hive.CreateSubKey($@"{UninstallRoot}\{Branding.UninstallKeyName}");

        var exe = Path.Combine(target, Branding.ExeName);
        var script = Path.Combine(target, Branding.UninstallScript);
        var sizeKb = (int)(new DirectoryInfo(target).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024);

        key.SetValue("DisplayName", Branding.IsSandbox ? $"{Branding.ProductName} (teste)" : Branding.ProductName);
        key.SetValue("DisplayVersion", Branding.Version);
        key.SetValue("Publisher", Branding.ProductName);
        key.SetValue("DisplayIcon", exe);
        key.SetValue("InstallLocation", target);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("UninstallString",
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"" + (Branding.IsSandbox ? " -Sandbox" : string.Empty));
    }

    /// <summary>Remove a instalação por usuário feita pelo instalador antigo (pasta em LocalAppData\Programs).</summary>
    private static void RemoveLegacyPerUserInstall(string target)
    {
        if (Branding.IsSandbox)
            return;

        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProductivityMonitor");
        if (string.Equals(Path.GetFullPath(legacy), target, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(legacy))
            return;

        DeleteQuietly(legacy);
        var oldShortcuts = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Branding.ShortcutName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Branding.ShortcutName)
        };
        foreach (var shortcut in oldShortcuts)
        {
            // Só apaga o atalho antigo se ele ainda apontar para a pasta removida.
            if (File.Exists(shortcut) && PointsInside(shortcut, legacy))
                File.Delete(shortcut);
        }
    }

    private static bool PointsInside(string shortcutPath, string folder)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return false;
            dynamic shell = Activator.CreateInstance(shellType)!;
            string targetPath = shell.CreateShortcut(shortcutPath).TargetPath;
            return targetPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ---- Utilitários ------------------------------------------------------------------------------------------

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception)
        {
            // Limpeza de sobras: não deve impedir a instalação.
        }
    }

    private static void TryMove(string from, string to)
    {
        try
        {
            Directory.Move(from, to);
        }
        catch (Exception)
        {
            // Sem como restaurar automaticamente; a pasta ".setup-old" permanece para recuperação manual.
        }
    }
}

