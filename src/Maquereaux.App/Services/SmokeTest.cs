using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Maquereaux.App.Controls;
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

            await CaptureDesignSystemAsync();
            await CapturePagesAsync();
            await CaptureMacroStatesAsync();
            await CaptureOnboardingAsync();

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
        _vm.IsLightTheme = true;
        await Delay(300);
        Capture(_window, "0-running-light");
        _vm.IsLightTheme = false;
        var ticks = _host.Engine.TicksSent;
        var stats = _host.Recorder.Snapshot();
        Log($"Stats 10 ms ({_host.Engine.CurrentSleeperName}) : moyenne {stats.Mean:0.000} ms, p95 {stats.P95:0.000}, σ {stats.StdDev:0.000}, manqués {stats.Missed}");
        var state = _host.Engine.State;

        // Modification à chaud pendant l'exécution : l'intervalle passe à 20 ms sans arrêter la macro
        profile.IntervalMs = 20;
        await Delay(900);
        var live = _host.Engine.RunningProfile;
        var stats20 = _host.Recorder.Snapshot();
        Log($"Édition à chaud : intervalle en cours = {live?.IntervalMs} ms, moyenne mesurée {stats20.Mean:0.000} ms");
        if (live?.IntervalMs != 20) throw new InvalidOperationException("La modification à chaud n'a pas été appliquée");
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

        // Suppression sans confirmation + toast « Annuler » qui restaure le profil à sa place
        var victim = _vm.ActiveProfile;
        int count = _vm.Profiles.Count;
        _vm.DeleteProfileCommand.Execute(victim);
        await Delay(300);
        if (_vm.Profiles.Count != count - 1) throw new InvalidOperationException("Suppression de profil KO");
        Capture(_window, "4b-profil-supprime-toast");
        var undo = _vm.Notifications.LastOrDefault(n => n.ActionCommand is not null)
                   ?? throw new InvalidOperationException("Toast « Annuler » absent");
        undo.ActionCommand!.Execute(null);
        await Delay(200);
        if (_vm.Profiles.Count != count || !_vm.Profiles.Contains(victim!)) throw new InvalidOperationException("Annulation de suppression KO");
        Log("Suppression + Annuler : OK");

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

        // Compte à rebours géant (capturé puis annulé)
        lab.UseCountdown = true;
        lab.ToggleRecordCommand.Execute(null);
        await Delay(400);
        if (!lab.IsCountingDown) throw new InvalidOperationException("Le compte à rebours n'a pas démarré");
        Capture(_window, "labo-compte-a-rebours");
        lab.ToggleRecordCommand.Execute(null);
        await Delay(200);
        if (lab.IsCountingDown || lab.IsRecording) throw new InvalidOperationException("Annulation du compte à rebours KO");

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
        Log($"Verdict A/B : « {lab.Verdict} »");

        var chart = FindVisual<IntervalChart>(_window) ?? throw new InvalidOperationException("Graphique du Labo introuvable");
        chart.BringIntoView();
        await Delay(300);
        Capture(_window, "7a-labo-graphique");
        chart.Mode = ChartMode.Histogram;
        await Delay(300);
        Capture(_window, "7b-labo-histogramme");
        chart.Mode = ChartMode.Scatter;
        if (FindVisualByName(_window, "VerdictBanner") is FrameworkElement verdict)
        {
            verdict.BringIntoView(new Rect(0, 0, verdict.ActualWidth, 420));
            await Delay(300);
            Capture(_window, "7d-labo-comparaison");
        }
        _vm.IsLightTheme = true;
        await Delay(300);
        Capture(_window, "7c-labo-clair");
        _vm.IsLightTheme = false;
    }

    private void SelectTab(string header)
    {
        var tab = _window.Tabs.Items.OfType<TabItem>().First(t => (string)t.Header == header);
        _window.Tabs.SelectedItem = tab;
    }

    /// <summary>Galerie du design system, dans les deux thèmes, à 100 % et 150 %.</summary>
    private async Task CaptureDesignSystemAsync()
    {
        var gallery = new Window
        {
            Width = 1180, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, ShowInTaskbar = false,
            Left = 0, Top = 0, Content = new DesignSystemView(),
        };
        gallery.SetResourceReference(Window.BackgroundProperty, "Bg.Layer");
        gallery.Show();
        foreach (var light in new[] { false, true })
        {
            _vm.IsLightTheme = light;
            await Delay(400);
            var theme = light ? "light" : "dark";
            Capture(gallery, $"ds-{theme}");
            if (!light) Capture(gallery, $"ds-{theme}@150", 1.5);
        }
        _vm.IsLightTheme = false;
        gallery.Close();
        Log("Galerie du design system : OK");
    }

    /// <summary>Chaque page dans chaque thème (+ 150 % et taille minimale en sombre).</summary>
    private async Task CapturePagesAsync()
    {
        var tabs = _window.Tabs.Items.OfType<TabItem>().ToList();
        foreach (var light in new[] { false, true })
        {
            _vm.IsLightTheme = light;
            var theme = light ? "light" : "dark";
            for (int i = 0; i < tabs.Count; i++)
            {
                _window.Tabs.SelectedIndex = i;
                await Delay(450);
                var page = Sanitize(tabs[i].Header?.ToString() ?? "tab");
                Capture(_window, $"{theme}-{i + 1}-{page}");
                if (!light && i == 0) Capture(_window, $"{theme}-{i + 1}-{page}@150", 1.5);
                Log($"Page « {tabs[i].Header} » ({theme}) OK");
            }
        }
        _vm.IsLightTheme = false;

        // Taille minimale : rien ne doit déborder ni être tronqué
        var (w, h) = (_window.Width, _window.Height);
        _window.Width = _window.MinWidth;
        _window.Height = _window.MinHeight;
        _window.Tabs.SelectedIndex = 0;
        await Delay(500);
        Capture(_window, "dark-min-macro");
        _vm.IsSidebarCollapsed = true;
        await Delay(400);
        Capture(_window, "dark-min-sidebar-repliee");
        _vm.IsSidebarCollapsed = false;
        _window.Width = w;
        _window.Height = h;
        await Delay(300);
    }

    /// <summary>États particuliers de l'écran Macro : conseil d'appui, capture d'une touche, pause automatique.</summary>
    private async Task CaptureMacroStatesAsync()
    {
        _window.Tabs.SelectedIndex = 0;
        var profile = _vm.ActiveProfile ?? throw new InvalidOperationException("Pas de profil actif");
        var target = profile.TargetProcess;

        // Cible = probablement un jeu + touches à 0 ms → bandeau « Appliquer 16 ms »
        profile.TargetProcess = "jeu-introuvable.exe";
        await Delay(350);
        if (!profile.ShowHoldAdvice) throw new InvalidOperationException("Le conseil d'appui ne s'affiche pas");
        Capture(_window, "macro-conseil-appui");

        // Mode capture de la tuile « + »
        var editor = FindVisual<SequenceEditor>(_window) ?? throw new InvalidOperationException("Éditeur de séquence introuvable");
        editor.BeginAdd();
        await Delay(250);
        Capture(_window, "macro-ajout-touche");
        _window.Nav.Focus(); // le focus quitte l'éditeur → capture annulée, raccourcis réactivés
        await Delay(150);
        if (editor.IsAdding) throw new InvalidOperationException("La capture de touche ne s'est pas annulée");

        // Fenêtre cible absente → le moteur démarre en pause automatique (aucune touche envoyée)
        _vm.StartStopCommand.Execute(null);
        await Delay(700);
        Log($"Cible absente : état moteur = {_host.Engine.State}");
        Capture(_window, "macro-pause");
        _vm.StartStopCommand.Execute(null);
        await Delay(300);

        profile.TargetProcess = target;
        Log("États de l'écran Macro : OK");
    }

    /// <summary>Visite guidée : première et dernière étape, puis « Passer » doit la marquer comme vue.</summary>
    private async Task CaptureOnboardingAsync()
    {
        _vm.StartOnboardingCommand.Execute(null);
        await Delay(350);
        Capture(_window, "onboarding-1");
        _vm.NextOnboardingCommand.Execute(null);
        _vm.NextOnboardingCommand.Execute(null);
        _vm.IsLightTheme = true;
        await Delay(350);
        Capture(_window, "onboarding-3-clair");
        _vm.IsLightTheme = false;
        if (!_vm.IsLastOnboardingStep) throw new InvalidOperationException("Visite guidée : dernière étape non atteinte");
        _vm.NextOnboardingCommand.Execute(null);
        await Delay(150);
        if (_vm.IsOnboardingVisible) throw new InvalidOperationException("Visite guidée non refermée");
        Log("Visite guidée : OK");
    }

    private static DependencyObject? FindVisualByName(DependencyObject root, string name)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: var n } && n == name) return child;
            if (FindVisualByName(child, name) is { } found) return found;
        }
        return null;
    }

    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FindVisual<T>(child) is { } found) return found;
        }
        return null;
    }

    /// <summary>Capture PNG d'une fenêtre ; <paramref name="scale"/> = 1,5 simule un écran à 150 % de DPI.</summary>
    private void Capture(Window window, string name, double scale = 1.0)
    {
        if (window.Content is not FrameworkElement root || root.ActualWidth < 1) return;
        var dpi = VisualTreeHelper.GetDpi(window);
        double sx = dpi.DpiScaleX * scale, sy = dpi.DpiScaleY * scale;
        var rtb = new RenderTargetBitmap(
            (int)(root.ActualWidth * sx), (int)(root.ActualHeight * sy),
            96 * sx, 96 * sy, PixelFormats.Pbgra32);
        // Fond explicite : sur Mica la fenêtre est transparente (le PNG sortirait noir/transparent)
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var bg = window.TryFindResource("Bg.Base") as Brush ?? window.Background;
            dc.DrawRectangle(bg, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
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
