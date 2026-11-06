using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace Productivity.Installer;

internal static class Elevation
{
    private const int ErrorCancelled = 1223;

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>Verdadeiro quando não é possível gravar na pasta sem permissão de administrador.</summary>
    public static bool NeedsElevation(string directory)
    {
        if (IsElevated)
            return false;

        try
        {
            var probe = directory;
            while (!Directory.Exists(probe))
            {
                var parent = Path.GetDirectoryName(probe);
                if (string.IsNullOrEmpty(parent))
                    return false;
                probe = parent;
            }

            var file = Path.Combine(probe, $".setup-{Guid.NewGuid():N}.tmp");
            using (File.Create(file))
            {
            }

            File.Delete(file);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (Exception)
        {
            return false; // outros erros aparecem com mensagem própria durante a instalação
        }
    }

    /// <summary>Reabre o instalador pedindo permissão de administrador. Retorna falso se o usuário recusar.</summary>
    public static bool RelaunchElevated(IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        try
        {
            Process.Start(info);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    /// <summary>Abre o programa instalado sem herdar privilégios de administrador.</summary>
    public static void LaunchApp(string exePath)
    {
        if (IsElevated)
        {
            // O Explorer roda sem elevação, então o programa também abre sem elevação.
            Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"\"{exePath}\"", UseShellExecute = false });
            return;
        }

        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exePath)! });
    }
}
