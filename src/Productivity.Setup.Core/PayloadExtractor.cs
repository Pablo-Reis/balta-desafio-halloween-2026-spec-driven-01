using System.IO.Compression;

namespace Productivity.Setup.Core;

public readonly record struct ExtractProgress(long BytesDone, long BytesTotal, int FilesDone, int FilesTotal, string CurrentFile)
{
    /// <summary>Progresso de 0 a 1 medido em bytes.</summary>
    public double Fraction => BytesTotal <= 0 ? 1 : Math.Clamp((double)BytesDone / BytesTotal, 0, 1);
}

/// <summary>Extrai o pacote (.zip) do programa para uma pasta, informando o progresso.</summary>
public static class PayloadExtractor
{
    private const int BufferSize = 81920;

    /// <summary>Soma o tamanho descompactado dos arquivos do pacote.</summary>
    public static long TotalSize(Stream zip)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        return archive.Entries.Where(e => e.Name.Length > 0).Sum(e => e.Length);
    }

    public static async Task ExtractAsync(Stream zip, string destination, IProgress<ExtractProgress>? progress, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        var files = archive.Entries.Where(e => e.Name.Length > 0).ToList();
        var totalBytes = files.Sum(f => f.Length);

        Directory.CreateDirectory(destination);
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        long done = 0;
        var filesDone = 0;
        progress?.Report(new ExtractProgress(0, totalBytes, 0, files.Count, string.Empty));

        foreach (var entry in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Proteção contra entradas com "..\" que escapariam da pasta de destino.
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O pacote de instalação contém um caminho inválido.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using (var source = entry.Open())
            await using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    done += read;
                    progress?.Report(new ExtractProgress(done, totalBytes, filesDone, files.Count, entry.FullName));
                }
            }

            filesDone++;
            progress?.Report(new ExtractProgress(done, totalBytes, filesDone, files.Count, entry.FullName));
        }
    }
}
