using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Productivity.Application.Abstractions;
using Productivity.Application.Persistence;
using Serilog;

namespace Productivity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

        services.AddLogging(builder =>
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File(
                    Path.Combine(AppPaths.LogsDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            builder.AddSerilog(logger, dispose: true);
        });

        services.AddSingleton<IAutoStartManager, RegistryAutoStartManager>();
        services.AddSingleton<IHotkeyRegistrar, Win32HotkeyRegistrar>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<INotificationSender>(sp => sp.GetRequiredService<TrayIconService>());
        return services;
    }

    /// <summary>Cria o banco local caso ainda não exista.</summary>
    public static void EnsureDatabaseCreated(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }
}
