using Microsoft.Extensions.DependencyInjection;
using Productivity.Application.Abstractions;
using Productivity.Application.Services;

namespace Productivity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDemandService, DemandService>();
        services.AddSingleton<IDemandEventService, DemandEventService>();
        services.AddSingleton<INoteService, NoteService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<BreakMonitor>();
        return services;
    }
}
