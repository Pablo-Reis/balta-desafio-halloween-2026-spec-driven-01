using Productivity.Setup.Core;

namespace Productivity.Setup.Tests;

public class InstallPlannerTests
{
    [Theory]
    [InlineData(null, "1.0.0", InstallKind.Install)]
    [InlineData("", "1.0.0", InstallKind.Install)]
    [InlineData("1.0.0", "1.1.0", InstallKind.Update)]
    [InlineData("1.0.0", "1.0.1", InstallKind.Update)]
    [InlineData("1.9.0", "1.10.0", InstallKind.Update)]
    [InlineData("1.0.0", "1.0.0", InstallKind.Reinstall)]
    [InlineData("1.0", "1.0.0.0", InstallKind.Reinstall)]
    [InlineData("2.0.0", "1.5.0", InstallKind.Downgrade)]
    public void Determine_ComparesVersions(string? installed, string incoming, InstallKind expected)
    {
        Assert.Equal(expected, InstallPlanner.Determine(installed, incoming));
    }

    [Theory]
    [InlineData("1.0.0+abc123", "1.0.0", InstallKind.Reinstall)]
    [InlineData("1.0.0-beta", "1.0.0", InstallKind.Reinstall)]
    public void Determine_IgnoresBuildMetadataAndPreRelease(string installed, string incoming, InstallKind expected)
    {
        Assert.Equal(expected, InstallPlanner.Determine(installed, incoming));
    }

    [Fact]
    public void Determine_WithUnreadableInstalledVersion_TreatsAsUpdate()
    {
        Assert.Equal(InstallKind.Update, InstallPlanner.Determine("lixo", "1.0.0"));
    }
}

public class InstallPathRulesTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Monitor de produtividade")]
    [InlineData(@"D:\Apps\Monitor")]
    [InlineData(@"  C:\Apps\Monitor  ")]
    public void Validate_AcceptsNormalFolders(string path)
    {
        Assert.Null(InstallPathRules.Validate(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmpty(string? path)
    {
        Assert.Equal("Informe a pasta de instalação.", InstallPathRules.Validate(path));
    }

    [Theory]
    [InlineData(@"Monitor")]
    [InlineData(@"..\Monitor")]
    [InlineData(@"Apps\Monitor")]
    public void Validate_RejectsRelativePaths(string path)
    {
        Assert.Contains("caminho completo", InstallPathRules.Validate(path));
    }

    [Theory]
    [InlineData(@"C:\Apps\Mon*itor")]
    [InlineData(@"C:\Apps\Mon?itor")]
    [InlineData(@"C:\Apps\Mon|itor")]
    [InlineData("C:\\Apps\\Mon\"itor")]
    public void Validate_RejectsInvalidCharacters(string path)
    {
        Assert.Contains("inválidos", InstallPathRules.Validate(path));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    public void Validate_RejectsDriveRoot(string path)
    {
        Assert.Contains("raiz", InstallPathRules.Validate(path));
    }

    [Fact]
    public void Validate_RejectsTooLongPaths()
    {
        var path = @"C:\" + new string('a', InstallPathRules.MaxLength);

        Assert.Contains("muito longo", InstallPathRules.Validate(path));
    }
}

public class SetupArgumentsTests
{
    [Fact]
    public void Parse_WithoutArguments_UsesDefaults()
    {
        var args = SetupArguments.Parse(Array.Empty<string>());

        Assert.False(args.Silent);
        Assert.False(args.Resume);
        Assert.False(args.NoDesktop);
        Assert.False(args.NoLaunch);
        Assert.Null(args.Directory);
    }

    [Fact]
    public void Parse_ReadsAllOptions_CaseInsensitive()
    {
        var args = SetupArguments.Parse(new[] { "/SILENT", "/Dir=C:\\Apps\\Monitor", "-nodesktop", "--nolaunch", "/resume" });

        Assert.True(args.Silent);
        Assert.True(args.NoDesktop);
        Assert.True(args.NoLaunch);
        Assert.True(args.Resume);
        Assert.Equal(@"C:\Apps\Monitor", args.Directory);
    }

    [Fact]
    public void Parse_StripsQuotesFromDirectory_AndIgnoresUnknownOptions()
    {
        var args = SetupArguments.Parse(new[] { "/dir=\"C:\\Program Files\\Monitor\"", "/qualquercoisa", "texto" });

        Assert.Equal(@"C:\Program Files\Monitor", args.Directory);
        Assert.False(args.Silent);
    }

    [Fact]
    public void Parse_WithEmptyDirectory_LeavesDirectoryNull()
    {
        Assert.Null(SetupArguments.Parse(new[] { "/dir=" }).Directory);
    }
}
