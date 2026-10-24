using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Maquereaux.App.ViewModels;
using Maquereaux.App.Views;

namespace Maquereaux.App.Services;

/// <summary>
/// Mode <c>--smoke-test</c> (CI) : parcourt tous les onglets, fait une capture PNG de chacun, lance
/// brièvement le moteur, puis ferme l'appli. Code de sortie 0 = tout s'est bien passé.
/// Les captures (dossier "screenshots" des données) servent aussi pour le README.
/// </summary>
internal sealed class SmokeTest
{
    private readonly MainWindow _window;
    private readonly MainViewModel _vm;
    private readonly AppHost _host;
    private readonly StringBuilder _report = new();
    private readonly string _shotsDir;

    public SmokeTest(MainWindow window, MainViewModel vm, AppHost host)
    {
        _window = window;
        _vm = vm;
        _host = host;
        _shotsDir = Path.Combine(host.Paths.DataDir, "screenshots");
        Directory.CreateDirectory(_shotsDir);
    }

    public int ExitCode { get; private set; } = 1;

    public async Task RunAsync()
    {
        try
        {
            Log($"Version {Core.AppInfo.Version}, données {_host.Paths.DataDir}");
            await Delay(800);

            var tabs = _window.Tabs.Items.OfType<TabItem>().ToList();
            for (int i = 0; i < tabs.Count; i++)
            {
                _window.Tabs.SelectedIndex = i;
                await Delay(500);
                var name = $"{i + 1}-{Sanitize(tabs[i].Header?.ToString() ?? "tab")}";
                Capture(_window, name);
                Log($"Onglet « {tabs[i].Header} » OK");
            }

            await RunExtraChecksAsync();

            ExitCode = 0;
            Log("SMOKE OK");
        }
        catch (Exception ex)
        {
            Log("SMOKE FAIL : " + ex);
            ExitCode = 1;
        }
        finally
        {
            WriteReport();
        }
    }

    /// <summary>Démarre le moteur 1 s sur F24 (touche sans effet) et vérifie qu'il envoie.</summary>
    private async Task RunExtraChecksAsync()
    {
        _window.Tabs.SelectedIndex = 0;
        var profile = _vm.ActiveProfile ?? throw new InvalidOperationException("Pas de profil actif");
        var original = profile.Model.Clone();

        profile.ClearStepsCommand.Execute(null);
        profile.AddStepCommand.Execute(Core.Models.KeyStroke.FromVirtualKey(Core.Input.VirtualKeys.F24));
        profile.IntervalMs = 10;
        await Delay(200);

        _vm.StartStopCommand.Execute(null);
        await Delay(1000);
        Capture(_window, "0-running");
        var ticks = _host.Engine.TicksSent;
        var state = _host.Engine.State;
        _vm.StartStopCommand.Execute(null);
        await Delay(500);

        Log($"Moteur : état pendant le test = {state}, ticks en 1 s = {ticks}, refusés = {_host.Sender.FailedEvents}");
        if (state == Core.Engine.EngineState.Stopped) throw new InvalidOperationException("Le moteur n'a pas démarré");
        if (ticks < 20) throw new InvalidOperationException($"Trop peu de ticks ({ticks})");
        if (_host.Engine.IsRunning) throw new InvalidOperationException("Le moteur ne s'est pas arrêté");

        // Restaure le profil d'origine (le smoke test ne doit rien laisser derrière lui)
        profile.ClearStepsCommand.Execute(null);
        foreach (var s in original.Steps) profile.AddStepCommand.Execute(s.Key);
        profile.IntervalMs = original.IntervalMs;
    }

    private void Capture(Window window, string name)
    {
        if (window.Content is not FrameworkElement root || root.ActualWidth < 1) return;
        var dpi = VisualTreeHelper.GetDpi(window);
        var rtb = new RenderTargetBitmap(
            (int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        // Fond explicite (sinon les zones transparentes sortent noires dans le PNG)
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(_shotsDir, name + ".png"));
        enc.Save(fs);
    }

    private static string Sanitize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static Task Delay(int ms)
    {
        var tcs = new TaskCompletionSource();
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); tcs.SetResult(); };
        t.Start();
        return tcs.Task;
    }

    public void Log(string line) => _report.AppendLine($"{DateTime.Now:HH:mm:ss.fff} {line}");

    private void WriteReport()
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("MAQUEREAUX_SMOKE_REPORT")
                       ?? Path.Combine(_host.Paths.DataDir, "smoke-report.txt");
            File.WriteAllText(path, _report.ToString());
        }
        catch { /* rien à faire */ }
    }
}
