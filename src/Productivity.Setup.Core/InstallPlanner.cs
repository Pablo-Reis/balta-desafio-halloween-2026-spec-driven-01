namespace Productivity.Setup.Core;

public enum InstallKind
{
    /// <summary>Nenhuma versão instalada.</summary>
    Install,

    /// <summary>Há uma versão mais antiga instalada.</summary>
    Update,

    /// <summary>A mesma versão já está instalada.</summary>
    Reinstall,

    /// <summary>Há uma versão mais nova instalada.</summary>
    Downgrade
}

public static class InstallPlanner
{
    /// <summary>Decide se a execução é instalação, atualização, reinstalação ou retrocesso de versão.</summary>
    public static InstallKind Determine(string? installedVersion, string newVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
            return InstallKind.Install;

        // Versão instalada ilegível (registro alterado): trata como atualização para sobrescrever com segurança.
        if (!TryParse(installedVersion, out var installed) || !TryParse(newVersion, out var incoming))
            return InstallKind.Update;

        var comparison = incoming.CompareTo(installed);
        return comparison > 0 ? InstallKind.Update : comparison == 0 ? InstallKind.Reinstall : InstallKind.Downgrade;
    }

    /// <summary>Aceita "1.2.3", "1.2.3.0", "1.2.3+commit" e "1.2.3-beta".</summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var core = text.Trim();
        var cut = core.IndexOfAny(new[] { '+', '-' });
        if (cut >= 0)
            core = core[..cut];

        if (!Version.TryParse(core, out var parsed))
            return false;

        // Normaliza 1.2 e 1.2.0 e 1.2.0.0 para a mesma versão.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }
}
