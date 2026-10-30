using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Productivity.Application.Persistence;
using Productivity.Application.Services;

namespace Productivity.Application.Tests;

/// <summary>Regras de arquitetura da constituição verificadas automaticamente.</summary>
public class ArchitectureTests
{
    [Fact]
    public void OnlyServices_DependOnTheDatabase()
    {
        var offenders = typeof(IDemandService).Assembly.GetTypes()
            .Where(t => t != typeof(AppDbContext) && !t.IsNested)
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType == typeof(IDbContextFactory<AppDbContext>) || p.ParameterType == typeof(AppDbContext)))
            .Where(t => !t.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryModel_HasItsOwnService()
    {
        var services = typeof(IDemandService).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet();

        Assert.Contains("IDemandService", services);
        Assert.Contains("IDemandEventService", services);
        Assert.Contains("INoteService", services);
        Assert.Contains("ISettingsService", services);
    }

    [Theory]
    [InlineData("src/Productivity.App")]
    [InlineData("src/Productivity.Infrastructure")]
    public void UiAndInfrastructure_NeverTouchTheDatabaseDirectly(string relativeFolder)
    {
        var folder = Path.Combine(FindRepositoryRoot(), relativeFolder.Replace('/', Path.DirectorySeparatorChar));

        // DependencyInjection.cs é a raiz de composição: registra o contexto, mas não o consulta.
        var offenders = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && Path.GetFileName(f) != "DependencyInjection.cs")
            .Where(f => File.ReadAllText(f).Contains("DbContext", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.slnx").Any())
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
