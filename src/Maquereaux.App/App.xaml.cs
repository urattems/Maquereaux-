using System.Windows;
using System.Windows.Threading;
using Maquereaux.App.Services;
using Maquereaux.App.ViewModels;
using Maquereaux.App.Views;
using Maquereaux.Core;
using Microsoft.Extensions.Logging;

namespace Maquereaux.App;

public partial class App : Application
{
    private AppHost? _host;
    private MainViewModel? _vm;
    private ILoggerFactory? _loggerFactory;
    private ILogger? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool smoke = e.Args.Contains("--smoke-test");

        var paths = AppPaths.Default();
        paths.EnsureCreated();

        _loggerFactory = LoggerFactory.Create(b => b.AddProvider(new FileLoggerProvider(paths.LogsDir)));
        _log = _loggerFactory.CreateLogger<App>();
        InstallCrashHandlers();

        _host = new AppHost(paths, _loggerFactory);
        _vm = new MainViewModel(_host, Dispatcher);

        var window = new MainWindow { DataContext = _vm };
        MainWindow = window;
        window.Show();
        _vm.Initialize();

        if (smoke) RunSmokeTest(window);
    }

    private async void RunSmokeTest(MainWindow window)
    {
        var smoke = new SmokeTest(window, _vm!, _host!);
        DispatcherUnhandledException += (_, args) =>
        {
            smoke.Log("Exception UI : " + args.Exception);
            args.Handled = true;
        };
        await smoke.RunAsync();
        Shutdown(smoke.ExitCode);
    }

    /// <summary>
    /// Filets de sécurité : quoi qu'il arrive, on relâche les touches enfoncées
    /// (anti « touche bloquée » après un crash) et on logue l'erreur.
    /// </summary>
    private void InstallCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _host?.PanicReleaseKeys();
            _log?.LogCritical(args.ExceptionObject as Exception, "Exception non gérée (fatale)");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            _host?.PanicReleaseKeys();
            _log?.LogError(args.Exception, "Exception UI");
            args.Handled = true;
            _vm?.Notify("Erreur inattendue : " + args.Exception.Message, NotificationLevel.Error);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _log?.LogError(args.Exception, "Tâche en échec non observée");
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Ordre important : arrêter le moteur (relâche les touches) AVANT de couper le reste
        _vm?.Dispose();
        _host?.Dispose();
        _loggerFactory?.Dispose();
        base.OnExit(e);
    }
}
