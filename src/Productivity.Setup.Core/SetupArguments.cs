namespace Productivity.Setup.Core;

/// <summary>Opções da linha de comando do instalador.</summary>
public sealed record SetupArguments
{
    /// <summary>/silent: instala sem janelas, com os valores padrão.</summary>
    public bool Silent { get; init; }

    /// <summary>/dir=CAMINHO: pasta de instalação.</summary>
    public string? Directory { get; init; }

    /// <summary>/nodesktop: não cria atalho na Área de Trabalho.</summary>
    public bool NoDesktop { get; init; }

    /// <summary>/nolaunch: não abre o programa ao terminar (modo silencioso).</summary>
    public bool NoLaunch { get; init; }

    /// <summary>/resume: segunda etapa após pedir permissão de administrador; começa direto na instalação.</summary>
    public bool Resume { get; init; }

    public static SetupArguments Parse(IEnumerable<string> args)
    {
        var result = new SetupArguments();

        foreach (var raw in args)
        {
            var arg = raw.Trim();
            if (arg.Length < 2 || (arg[0] != '/' && arg[0] != '-'))
                continue;

            var body = arg.TrimStart('/', '-');
            var separator = body.IndexOf('=');
            var name = (separator >= 0 ? body[..separator] : body).ToLowerInvariant();
            var value = separator >= 0 ? body[(separator + 1)..].Trim('"') : null;

            result = name switch
            {
                "silent" or "s" or "quiet" => result with { Silent = true },
                "dir" or "d" => result with { Directory = string.IsNullOrWhiteSpace(value) ? null : value },
                "nodesktop" => result with { NoDesktop = true },
                "nolaunch" => result with { NoLaunch = true },
                "resume" => result with { Resume = true },
                _ => result
            };
        }

        return result;
    }
}
