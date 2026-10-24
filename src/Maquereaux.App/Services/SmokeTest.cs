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
            await RunUiChecksAsync();
            await RunLabChecksAsync();

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
        var stats = _host.Recorder.Snapshot();
        Log($"Stats 10 ms ({_host.Engine.CurrentSleeperName}) : moyenne {stats.Mean:0.000} ms, p95 {stats.P95:0.000}, σ {stats.StdDev:0.000}, manqués {stats.Missed}");
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

    /// <summary>Profils, persistance, thème clair, mode compact.</summary>
    private async Task RunUiChecksAsync()
    {
        int before = _vm.Profiles.Count;
        _vm.NewProfileCommand.Execute(null);
        _vm.DuplicateProfileCommand.Execute(null);
        if (_vm.Profiles.Count != before + 2) throw new InvalidOperationException("Création de profil KO");
        _vm.ActiveProfile!.ActivationMode = Core.Models.ActivationMode.Hold;
        _vm.ActiveProfile.Hotkey = new Core.Models.Hotkey(Core.Models.HotkeyModifiers.None, Core.Input.VirtualKeys.F8);
        _vm.ActiveProfile.TargetProcess = "notepad";
        SelectTab("Profils");
        await Delay(400);
        Capture(_window, "4-profils-multiples");

        _vm.SaveNow();
        var repo = new Core.Profiles.ProfileRepository(_host.Paths);
        var reloaded = repo.LoadProfiles();
        if (reloaded.Count != _vm.Profiles.Count) throw new InvalidOperationException("Persistance KO");
        if (reloaded.Last().TargetProcess != "notepad.exe") throw new InvalidOperationException("Process cible non sauvegardé");
        Log($"Profils : {reloaded.Count} sauvegardés et relus, hook installé = {_host.Hook.IsInstalled}");
        if (!_host.Hook.IsInstalled) throw new InvalidOperationException("Hook non installé alors qu'un profil Maintenir existe");

        _vm.IsLightTheme = true;
        SelectTab("Macro");
        await Delay(400);
        Capture(_window, "5-theme-clair");
        _vm.IsLightTheme = false;

        _vm.IsCompact = true;
        await Delay(500);
        var compact = Application.Current.Windows.OfType<CompactWindow>().FirstOrDefault()
                      ?? throw new InvalidOperationException("Fenêtre compacte absente");
        Capture(compact, "6-compact");
        _vm.IsCompact = false;
        await Delay(300);
        Log("Thème clair, mode compact : OK");
    }

    /// <summary>
    /// Labo : session de 2 s pendant laquelle le moteur envoie F24 à 10 ms → les stats « Maquereaux »
    /// doivent être cohérentes (≈ 100 inputs/s, intervalle ≈ 10 ms). Puis export CSV et comparaison.
    /// </summary>
    private async Task RunLabChecksAsync()
    {
        var lab = _vm.Lab;
        SelectTab("Labo");
        lab.UseCountdown = false;
        lab.UseFixedDuration = true;
        lab.DurationSeconds = 2;

        // Session 1 : à vide (personne ne tape sur la VM de CI)
        lab.BeginRecording();
        await Delay(2300);
        if (lab.IsRecording) throw new InvalidOperationException("La session à durée fixe ne s'est pas arrêtée");

        // Session 2 : Maquereaux à 10 ms
        var profile = _vm.ActiveProfile!;
        profile.ActivationMode = Core.Models.ActivationMode.Toggle;
        profile.TargetProcess = null;
        profile.ClearStepsCommand.Execute(null);
        profile.AddStepCommand.Execute(Core.Models.KeyStroke.FromVirtualKey(Core.Input.VirtualKeys.F24));
        profile.IntervalMs = 10;
        await Delay(200);
        lab.BeginRecording();
        _vm.StartStopCommand.Execute(null);
        await Delay(2300);
        _vm.StartStopCommand.Execute(null);
        await Delay(300);
        if (lab.IsRecording) throw new InvalidOperationException("Session 2 non terminée");
        if (_host.Hook.IsInstalled && !_vm.Profiles.Any(p => p.ActivationMode == Core.Models.ActivationMode.Hold))
            throw new InvalidOperationException("Hook resté installé après la session (confidentialité)");

        var s = lab.SelectedSession ?? throw new InvalidOperationException("Pas de session");
        var ours = s.Results.For(Core.Lab.InputSource.Maquereaux) ?? throw new InvalidOperationException("Aucun input Maquereaux capturé");
        Log($"Labo : {ours.Inputs} inputs Maquereaux en {s.Results.DurationSeconds:0.0} s ({ours.InputsPerSecond:0.0}/s), intervalle moyen {ours.Intervals.Mean:0.000} ms, p95 {ours.Intervals.P95:0.000}, σ {ours.Intervals.StdDev:0.000}");
        if (ours.InputsPerSecond < 50) throw new InvalidOperationException("Cadence capturée incohérente");
        if (lab.ComparisonRows.Count == 0) throw new InvalidOperationException("Comparaison vide");

        var csv = Path.Combine(_host.Paths.DataDir, "smoke-session.csv");
        Core.Lab.CsvExporter.Export(s.Session, csv);
        var lines = File.ReadAllLines(csv);
        Log($"CSV : {lines.Length} lignes, en-tête « {lines[0]} »");
        if (lines.Length < ours.Inputs) throw new InvalidOperationException("CSV incomplet");

        s.Note = "Smoke test CI : F24 à 10 ms pendant 2 s";
        await Delay(500);
        Capture(_window, "7-labo-resultats");
    }

    private void SelectTab(string header)
    {
        var tab = _window.Tabs.Items.OfType<TabItem>().First(t => (string)t.Header == header);
        _window.Tabs.SelectedItem = tab;
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
