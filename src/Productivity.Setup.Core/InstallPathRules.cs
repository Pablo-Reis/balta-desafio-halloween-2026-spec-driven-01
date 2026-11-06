namespace Productivity.Setup.Core;

public static class InstallPathRules
{
    public const int MaxLength = 200;

    /// <summary>Devolve a mensagem de erro (pt-BR) ou null quando a pasta é aceitável.</summary>
    public static string? Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Informe a pasta de instalação.";

        path = path.Trim();

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.Contains('*') || path.Contains('?') || path.Contains('"')
            || path.Contains('<') || path.Contains('>') || path.Contains('|'))
            return "O caminho contém caracteres inválidos.";

        if (!Path.IsPathFullyQualified(path))
            return "Use um caminho completo, por exemplo C:\\Arquivos de Programas\\Monitor de produtividade.";

        if (path.Length > MaxLength)
            return $"O caminho é muito longo (máximo de {MaxLength} caracteres).";

        var full = Path.GetFullPath(path);
        if (string.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase))
            return "Escolha uma pasta dentro do disco, não a raiz dele.";

        return null;
    }
}
