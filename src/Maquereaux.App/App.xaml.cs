using System.Security.Cryptography;
using System.Text;
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
    private TrayIcon? _tray;
    private ILoggerFactory? _loggerFactory;
    private ILogger? _log;
    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private bool _startupComplete;

    public App()
    {
        // Filet le plus précoce possible (avant même le chargement des ressources XAML) : en smoke test, un crash
        // est écrit dans le rapport et l'appli quitte avec un code d'erreur au lieu d'ouvrir la boîte de dialogue
        // Windows (qui bloquerait la CI jusqu'au timeout).
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var report = Environment.GetEnvironmentVariable("MAQUEREAUX_SMOKE_REPORT");
            if (report is null) return;
            try { File.AppendAllText(report, $"CRASH : {args.ExceptionObject}{Environment.NewLine}"); } catch { }
            Environment.Exit(4);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool smoke = e.Args.Contains("--smoke-test");

        var paths = AppPaths.Default();
        paths.EnsureCreated();

        // Une seule instance par dossier de données (deux instances = conflits de raccourcis).
        // La 2e instance demande simplement à la 1re de se montrer, puis se ferme.
        if (!AcquireSingleInstance(paths.DataDir))
        {
            Shutdown(0);
            return;
        }

        _loggerFactory = LoggerFactory.Create(b => b.AddProvider(new FileLoggerProvider(paths.LogsDir)));
        _log = _loggerFactory.CreateLogger<App>();
        InstallCrashHandlers();

        _host = new AppHost(paths, _loggerFactory);
        _vm = new MainViewModel(_host, Dispatcher, new DialogService());
        _vm.ThemeChanged += ApplyTheme;
        ApplyTheme(_vm.IsLightTheme);

        var window = new MainWindow { DataContext = _vm };
        if (!smoke) window.RestorePlacement();
        MainWindow = window;
        window.Show();
        _vm.Initialize();

        _tray = new TrayIcon(_vm, window.ShowFromTray, () => window.Close());

        _startupComplete = true;
        if (smoke) RunSmokeTest(window);
    }

    private bool AcquireSingleInstance(string dataDir)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataDir.ToLowerInvariant())))[..16];
        _singleInstance = new Mutex(initiallyOwned: true, $"Local\\Maquereaux-{id}", out bool created);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\Maquereaux-show-{id}");
        if (!created)
        {
            _showSignal.Set();
            return false;
        }
        // Thread d'écoute : si une 2e instance est lancée, on remet notre fenêtre au premier plan
        var listener = new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.ShowFromTray());
        }) { IsBackground = true, Name = "Maquereaux.SingleInstance" };
        listener.Start();
        return true;
    }

    /// <summary>Remplace la palette de couleurs (index 0 des dictionnaires fusionnés) : thème changé à chaud.</summary>
    private void ApplyTheme(bool light)
    {
        var dict = new ResourceDictionary { Source = new Uri(light ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative) };
        Resources.MergedDictionaries[0] = dict;
        Controls.ThemeEvents.Raise();
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
            if (Environment.GetEnvironmentVariable("MAQUEREAUX_SMOKE_REPORT") is { } report && !_startupComplete)
            {
                // Exception pendant le démarrage en smoke test : on la rapporte et on sort (sinon l'appli resterait sans fenêtre)
                try { File.AppendAllText(report, $"CRASH AU DÉMARRAGE : {args.Exception}{Environment.NewLine}"); } catch { }
                args.Handled = true;
                Environment.Exit(5);
                return;
            }
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
        _tray?.Dispose();
        _vm?.Dispose();
        _host?.Dispose();
        _loggerFactory?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
