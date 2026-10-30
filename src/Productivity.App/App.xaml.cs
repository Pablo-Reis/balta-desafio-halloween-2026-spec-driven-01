using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Productivity.App.Support;
using Productivity.Application;
using Productivity.Infrastructure;

namespace Productivity.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "ProductivityMonitor.SingleInstance";
    private const string ShowEventName = "ProductivityMonitor.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var isFirstInstance);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!isFirstInstance)
        {
            // Já está rodando (provavelmente na bandeja): apenas pede para mostrar a janela.
            _showEvent.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            UiGuard.Handle(args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            UiGuard.Logger?.LogError(args.Exception, "Falha não observada em tarefa.");
            args.SetObserved();
        };

        var minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

        UiGuard.Run(async () =>
        {
            try
            {
                _services = new ServiceCollection()
                    .AddApplicationServices()
                    .AddInfrastructure()
                    .AddSingleton<AppController>()
                    .BuildServiceProvider();

                UiGuard.Logger = _services.GetRequiredService<ILoggerFactory>().CreateLogger("Aplicacao");
                Productivity.Infrastructure.DependencyInjection.EnsureDatabaseCreated(_services);

                var controller = _services.GetRequiredService<AppController>();
                ListenForShowRequests(controller);
                await controller.StartAsync(minimized);
            }
            catch (Exception ex)
            {
                UiGuard.Handle(ex);
                Shutdown(1);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _showEvent?.Dispose();
        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex não pertence a esta instância (segunda execução encerrada cedo).
            }

            _mutex.Dispose();
        }

        base.OnExit(e);
    }

    private void ListenForShowRequests(AppController controller)
    {
        var thread = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
                Dispatcher.BeginInvoke(controller.ShowMainWindow);
        })
        {
            IsBackground = true,
            Name = "ShowWindowListener"
        };
        thread.Start();
    }
}
