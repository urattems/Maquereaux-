using System.Windows;
using System.Windows.Threading;

namespace Maquereaux.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Mode "smoke test" (utilisé par la CI) : l'appli démarre, affiche la fenêtre
        // puis se ferme toute seule. Code de sortie 0 = pas de crash au démarrage.
        if (e.Args.Contains("--smoke-test"))
        {
            DispatcherUnhandledException += (_, args) =>
            {
                Console.Error.WriteLine($"SMOKE FAIL: {args.Exception}");
                args.Handled = true;
                Shutdown(1);
            };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) => { timer.Stop(); Shutdown(0); };
            timer.Start();
        }
    }
}
