using System.IO.Compression;
using System.Text;
using Productivity.Setup.Core;

namespace Productivity.Setup.Tests;

public sealed class PayloadExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "setup-tests-" + Guid.NewGuid().ToString("N"));

    public PayloadExtractorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static MemoryStream BuildZip(params (string Name, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private sealed class CollectingProgress : IProgress<ExtractProgress>
    {
        public List<ExtractProgress> Reports { get; } = new();

        public void Report(ExtractProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task Extract_CreatesFilesAndSubfolders()
    {
        using var zip = BuildZip(("ProductivityMonitor.exe", "exe"), ("runtimes/win/native.dll", "dll"), ("pt-BR/res.dll", "res"));
        var target = Path.Combine(_root, "app");

        await PayloadExtractor.ExtractAsync(zip, target, null, CancellationToken.None);

        Assert.Equal("exe", File.ReadAllText(Path.Combine(target, "ProductivityMonitor.exe")));
        Assert.Equal("dll", File.ReadAllText(Path.Combine(target, "runtimes", "win", "native.dll")));
        Assert.Equal("res", File.ReadAllText(Path.Combine(target, "pt-BR", "res.dll")));
    }

    [Fact]
    public async Task Extract_OverwritesExistingFiles()
    {
        var target = Path.Combine(_root, "app");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.txt"), "antigo");
        using var zip = BuildZip(("a.txt", "novo"));

        await PayloadExtractor.ExtractAsync(zip, target, null, CancellationToken.None);

        Assert.Equal("novo", File.ReadAllText(Path.Combine(target, "a.txt")));
    }

    [Fact]
    public async Task Extract_ReportsProgressFromZeroToOne_InOrder()
    {
        using var zip = BuildZip(("a.txt", new string('a', 1000)), ("b.txt", new string('b', 3000)));
        var progress = new CollectingProgress();

        await PayloadExtractor.ExtractAsync(zip, Path.Combine(_root, "app"), progress, CancellationToken.None);

        Assert.Equal(0, progress.Reports[0].Fraction);
        Assert.Equal(1, progress.Reports[^1].Fraction);
        Assert.Equal(2, progress.Reports[^1].FilesDone);
        Assert.Equal(2, progress.Reports[^1].FilesTotal);
        Assert.Equal(4000, progress.Reports[^1].BytesTotal);
        Assert.Equal(progress.Reports.Select(r => r.Fraction).OrderBy(f => f), progress.Reports.Select(r => r.Fraction));
    }

    [Fact]
    public async Task Extract_RejectsEntriesThatEscapeTheDestination()
    {
        using var zip = BuildZip(("../fora.txt", "malicioso"));
        var target = Path.Combine(_root, "app");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            PayloadExtractor.ExtractAsync(zip, target, null, CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(_root, "fora.txt")));
    }

    [Fact]
    public async Task Extract_StopsWhenCancelled()
    {
        using var zip = BuildZip(("a.txt", "a"), ("b.txt", "b"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PayloadExtractor.ExtractAsync(zip, Path.Combine(_root, "app"), null, cts.Token));
    }

    [Fact]
    public void TotalSize_SumsUncompressedBytes_IgnoringFolders()
    {
        using var zip = BuildZip(("a.txt", "12345"), ("pasta/b.txt", "123"));

        Assert.Equal(8, PayloadExtractor.TotalSize(zip));
    }
}
