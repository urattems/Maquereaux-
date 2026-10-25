using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maquereaux.App.Services;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Lab;

namespace Maquereaux.App.ViewModels;

/// <summary>
/// Onglet Labo : sessions d'enregistrement pour comparer objectivement manuel vs automatique.
/// <para>
/// Confidentialité : le hook n'écoute que pendant une session lancée à la main (indicateur ● REC),
/// les données restent en mémoire et ne vont sur disque que sur export/sauvegarde explicite.
/// </para>
/// </summary>
public sealed partial class LabViewModel : ObservableObject, IDisposable
{
    private readonly AppHost _host;
    private readonly Dispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;
    private readonly LabRecorder _recorder = new();
    private readonly DispatcherTimer _tickTimer;
    private readonly DispatcherTimer _countdownTimer;
    private bool _hookAcquired;
    private int _sessionCounter;

    public LabViewModel(AppHost host, Dispatcher dispatcher, IDialogService dialogs, MainViewModel main)
    {
        _host = host;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _main = main;

        _recorder.Finished += s => _dispatcher.BeginInvoke(() => OnSessionFinished(s));
        _tickTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
        _tickTimer.Tick += (_, _) => OnTick();
        _countdownTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => OnCountdownTick();
        Sessions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSessions));
    }

    public ObservableCollection<LabSessionViewModel> Sessions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonLabel))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonLabel))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isCountingDown;

    [ObservableProperty]
    private int _countdown;

    [ObservableProperty]
    private bool _useCountdown = true;

    [ObservableProperty]
    private bool _useFixedDuration = true;

    [ObservableProperty]
    private int _durationSeconds = 20;

    [ObservableProperty]
    private string _elapsedText = "0,0 s";

    [ObservableProperty]
    private int _liveEvents;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private LabSessionViewModel? _selectedSession;

    [ObservableProperty]
    private LabSessionViewModel? _compareA;

    [ObservableProperty]
    private LabSessionViewModel? _compareB;

    [ObservableProperty]
    private IReadOnlyList<ComparisonRow> _comparisonRows = Array.Empty<ComparisonRow>();

    /// <summary>Verdict A/B en une phrase (vide tant que A et B ne sont pas choisies).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    private string _verdict = "";

    public bool HasComparison => Verdict.Length > 0;

    /// <summary>Intervalle du profil actif : ligne « cible » du graphique.</summary>
    public double TargetIntervalMs => _main.ActiveProfile?.IntervalMs ?? 0;

    partial void OnSelectedSessionChanged(LabSessionViewModel? value) => OnPropertyChanged(nameof(TargetIntervalMs));

    public bool HasSessions => Sessions.Count > 0;

    public bool HasSelection => SelectedSession is not null;
    public bool IsBusy => IsRecording || IsCountingDown;
    public string RecordButtonLabel => IsRecording ? "■  Arrêter" : IsCountingDown ? "Annuler" : "●  Enregistrer";

    partial void OnDurationSecondsChanged(int value)
    {
        var v = Math.Clamp(value, 1, 3600);
        if (v != value) DurationSeconds = v;
    }

    partial void OnCompareAChanged(LabSessionViewModel? value) => UpdateComparison();
    partial void OnCompareBChanged(LabSessionViewModel? value) => UpdateComparison();

    // ---------- Enregistrement ----------

    [RelayCommand]
    private void ToggleRecord()
    {
        if (IsCountingDown) { CancelCountdown(); return; }
        if (IsRecording) { _recorder.Stop(StopwatchClock.Instance.NowMs); return; }

        if (UseCountdown)
        {
            Countdown = 3;
            IsCountingDown = true;
            _countdownTimer.Start();
        }
        else
        {
            BeginRecording();
        }
    }

    private void OnCountdownTick()
    {
        Countdown--;
        if (Countdown > 0) return;
        _countdownTimer.Stop();
        IsCountingDown = false;
        BeginRecording();
    }

    private void CancelCountdown()
    {
        _countdownTimer.Stop();
        IsCountingDown = false;
        Countdown = 0;
    }

    /// <summary>Démarre la capture (public pour le smoke test).</summary>
    public void BeginRecording()
    {
        if (IsRecording) return;
        if (!_host.AcquireHook())
        {
            _main.Notify("Impossible d'installer le hook clavier : enregistrement annulé.", NotificationLevel.Error);
            return;
        }
        _hookAcquired = true;
        _host.Hook.KeyEvent += OnHookEvent;
        double? duration = UseFixedDuration ? DurationSeconds * 1000.0 : null;
        _recorder.Start(StopwatchClock.Instance.NowMs, $"Session {++_sessionCounter}", duration);
        IsRecording = true;
        LiveEvents = 0;
        ElapsedText = FormatSeconds(0);
        _tickTimer.Start();
    }

    // Thread du hook (dispatch) : juste transmettre à l'enregistreur (thread-safe)
    private void OnHookEvent(RawKeyEvent e) => _recorder.OnKeyEvent(e);

    private void OnTick()
    {
        var now = StopwatchClock.Instance.NowMs;
        _recorder.Tick(now);
        if (!IsRecording) return;
        ElapsedText = FormatSeconds(_recorder.Elapsed(now));
        LiveEvents = _recorder.EventCount;
    }

    private void OnSessionFinished(LabSession session)
    {
        _tickTimer.Stop();
        _host.Hook.KeyEvent -= OnHookEvent;
        if (_hookAcquired) { _host.ReleaseHook(); _hookAcquired = false; }
        IsRecording = false;
        LiveEvents = session.Events.Count;
        ElapsedText = FormatSeconds(session.DurationMs);

        var vm = new LabSessionViewModel(session);
        vm.Name = $"{session.Name} — {vm.DominantLabel}";
        var previous = Sessions.LastOrDefault();
        Sessions.Add(vm);
        SelectedSession = vm;
        // Comparaison automatique avec la session précédente (ex. manuel puis Maquereaux)
        if (previous is not null)
        {
            CompareA = previous;
            CompareB = vm;
        }
        else
        {
            CompareA = vm;
        }
    }

    private void UpdateComparison()
    {
        ComparisonRows = CompareA is not null && CompareB is not null
            ? SessionComparison.Compare(CompareA.Results, CompareB.Results)
            : Array.Empty<ComparisonRow>();
        Verdict = CompareA is not null && CompareB is not null ? LabLogic.Verdict(ComparisonRows, CompareA.Name, CompareB.Name) : "";
    }

    // ---------- Fichiers (toujours sur action explicite) ----------

    [RelayCommand]
    private void ExportCsv(LabSessionViewModel? s)
    {
        s ??= SelectedSession;
        if (s is null) return;
        var path = _dialogs.SaveFile("Exporter la session en CSV", DialogService.CsvFilter, SafeName(s.Name) + ".csv");
        if (path is null) return;
        try
        {
            CsvExporter.Export(s.Session, path, CultureInfo.CurrentCulture);
            _main.Notify($"CSV exporté : {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _main.Notify("Export impossible : " + ex.Message, NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void SaveSession(LabSessionViewModel? s)
    {
        s ??= SelectedSession;
        if (s is null) return;
        var path = _dialogs.SaveFile("Sauvegarder la session", DialogService.SessionFilter, SafeName(s.Name) + LabSessionStore.Extension);
        if (path is null) return;
        try
        {
            LabSessionStore.Save(path, s.Session);
            _main.Notify($"Session sauvegardée : {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _main.Notify("Sauvegarde impossible : " + ex.Message, NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void LoadSession()
    {
        var path = _dialogs.OpenFile("Ouvrir une session", DialogService.SessionFilter);
        if (path is null) return;
        try
        {
            var vm = new LabSessionViewModel(LabSessionStore.Load(path));
            Sessions.Add(vm);
            SelectedSession = vm;
        }
        catch (InvalidDataException ex)
        {
            _main.Notify(ex.Message, NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void DeleteSession(LabSessionViewModel? s)
    {
        s ??= SelectedSession;
        if (s is null) return;
        Sessions.Remove(s);
        if (CompareA == s) CompareA = null;
        if (CompareB == s) CompareB = null;
        if (SelectedSession == s) SelectedSession = Sessions.LastOrDefault();
    }

    private static string SafeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static string FormatSeconds(double ms) => (ms / 1000.0).ToString("0.0", CultureInfo.CurrentCulture) + " s";

    public void Dispose()
    {
        _tickTimer.Stop();
        _countdownTimer.Stop();
        if (IsRecording) _recorder.Stop(StopwatchClock.Instance.NowMs);
        _host.Hook.KeyEvent -= OnHookEvent;
        if (_hookAcquired) { _host.ReleaseHook(); _hookAcquired = false; }
    }
}
