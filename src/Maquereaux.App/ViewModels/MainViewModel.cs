using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maquereaux.App.Services;
using Maquereaux.Core;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Models;
using Maquereaux.Core.Stats;
using Maquereaux.Win32;

namespace Maquereaux.App.ViewModels;

/// <summary>ViewModel principal : état du moteur, profil actif, réglages, notifications.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppHost _host;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refreshTimer;
    private readonly AppSettings _settings;

    public MainViewModel(AppHost host, Dispatcher dispatcher)
    {
        _host = host;
        _dispatcher = dispatcher;
        _settings = new AppSettings();

        var profile = new ProfileViewModel(Profile.CreateDefault());
        Profiles.Add(profile);
        HookProfile(profile);
        _activeProfile = profile;
        _alwaysOnTop = _settings.AlwaysOnTop;
        _emergencyHotkey = _settings.EmergencyStopHotkey;
        _timerStrategy = _settings.TimerStrategy;
        _host.TimerStrategy = _timerStrategy;

        _host.EngineStateChanged += (_, e) => _dispatcher.BeginInvoke(() => OnEngineStateChanged(e));
        _host.Error += msg => _dispatcher.BeginInvoke(() => Notify(msg, NotificationLevel.Warning));
        _host.ProfileActivated += id => _dispatcher.BeginInvoke(() =>
        {
            var p = Profiles.FirstOrDefault(x => x.Id == id);
            if (p is not null) ActiveProfile = p;
        });
        KeyCaptureCoordinator.CaptureChanged += capturing => _host.SuspendProfileHotkeys(capturing);

        // Rafraîchissement des compteurs (4×/s) : le moteur ne pousse rien vers l'UI à chaque tick.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) => RefreshLiveData();
    }

    public string Title => $"Maquereaux {AppInfo.Version}";

    public ObservableCollection<ProfileViewModel> Profiles { get; } = new();
    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();

    [ObservableProperty]
    private ProfileViewModel? _activeProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(StartStopLabel))]
    private EngineState _engineState = EngineState.Stopped;

    [ObservableProperty]
    private string? _runningProfileName;

    [ObservableProperty]
    private long _ticksSent;

    [ObservableProperty]
    private string _lastStopInfo = "Prêt.";

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private Hotkey _emergencyHotkey;

    /// <summary>Stats de timing en direct (rafraîchies 4×/s pendant l'exécution).</summary>
    [ObservableProperty]
    private TimingStats _liveStats = TimingStats.Empty;

    /// <summary>Nom de la stratégie d'attente réellement utilisée par l'exécution en cours.</summary>
    [ObservableProperty]
    private string _activeTimerName = "—";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHybridTimer))]
    [NotifyPropertyChangedFor(nameof(TimerStrategyDescription))]
    private TimerStrategy _timerStrategy;

    public bool IsHybridTimer => TimerStrategy == TimerStrategy.Hybrid;
    public string TimerStrategyDescription => SleeperFactory.Describe(TimerStrategy);

    public bool IsRunning => EngineState != EngineState.Stopped;
    public string StartStopLabel => IsRunning ? "■  STOP" : "▶  START";

    /// <summary>À appeler une fois la fenêtre affichée : publie les profils et enregistre les hotkeys.</summary>
    public void Initialize()
    {
        foreach (var p in Profiles) _host.Profiles.Publish(p.Model);
        ApplyHotkeys();
    }

    // ---------- Commandes ----------

    [RelayCommand]
    private void StartStop()
    {
        if (IsRunning) _host.Stop();
        else if (ActiveProfile is not null) _host.Toggle(ActiveProfile.Id);
    }

    [RelayCommand]
    private void EmergencyStop() => _host.EmergencyStop();

    [RelayCommand]
    private void DismissNotification(NotificationViewModel? n)
    {
        if (n is not null) Notifications.Remove(n);
    }

    // ---------- Réglages ----------

    partial void OnAlwaysOnTopChanged(bool value) => _settings.AlwaysOnTop = value;

    partial void OnTimerStrategyChanged(TimerStrategy value)
    {
        _settings.TimerStrategy = value;
        _host.TimerStrategy = value;
        if (IsRunning) Notify("Nouvelle stratégie de timer : prise en compte au prochain démarrage.");
    }

    partial void OnEmergencyHotkeyChanged(Hotkey value)
    {
        if (value is null || !value.IsValid)
        {
            Notify("L'arrêt d'urgence doit avoir une touche valide : valeur par défaut restaurée.", NotificationLevel.Warning);
            EmergencyHotkey = Hotkey.DefaultEmergencyStop;
            return;
        }
        _settings.EmergencyStopHotkey = value;
        ApplyHotkeys();
    }

    // ---------- Interne ----------

    private void HookProfile(ProfileViewModel p)
    {
        p.Changed += (vm, hotkeysAffected) =>
        {
            _host.Profiles.Publish(vm.Model);
            if (hotkeysAffected) ApplyHotkeys();
        };
    }

    private void ApplyHotkeys()
    {
        var errors = _host.ApplyHotkeys(_settings);
        foreach (var e in errors) Notify(e, NotificationLevel.Warning);
    }

    private void OnEngineStateChanged(EngineStateChangedEventArgs e)
    {
        EngineState = _host.Engine.State;
        RunningProfileName = e.State == EngineState.Stopped ? null : e.Profile?.Name;
        if (IsRunning) _refreshTimer.Start();
        else _refreshTimer.Stop();
        RefreshLiveData();

        if (e.State == EngineState.Stopped)
        {
            LastStopInfo = e.Reason switch
            {
                StopReason.Completed => "Terminé : toutes les répétitions ont été jouées.",
                StopReason.Emergency => "Arrêt d'urgence.",
                StopReason.Error => "Arrêt sur erreur : " + e.Error?.Message,
                StopReason.SafetyTimeout => "Arrêt de sécurité : durée maximale atteinte.",
                StopReason.HoldReleased => "Touche relâchée.",
                StopReason.Replaced => LastStopInfo,
                _ => "Arrêté.",
            };
            if (e.Reason == StopReason.Error) Notify(LastStopInfo, NotificationLevel.Error);
            if (_host.Sender.FailedEvents > 0)
                Notify($"{_host.Sender.FailedEvents} événement(s) refusé(s) par Windows (fenêtre admin ? bureau sécurisé ?).", NotificationLevel.Warning);
        }
        else if (e.State == EngineState.Running)
        {
            LastStopInfo = $"En cours : « {e.Profile?.Name} »";
        }
        else if (e.State == EngineState.Paused)
        {
            LastStopInfo = "En pause : la fenêtre cible n'a pas le focus.";
        }
    }

    private void RefreshLiveData()
    {
        TicksSent = _host.Engine.TicksSent;
        LiveStats = _host.Recorder.Snapshot();
        ActiveTimerName = _host.Engine.CurrentSleeperName ?? ActiveTimerName;
    }

    public void Notify(string message, NotificationLevel level = NotificationLevel.Info)
    {
        // Pas de doublon : le même message déjà affiché n'est pas répété
        if (Notifications.Any(n => n.Message == message)) return;
        var n = new NotificationViewModel(message, level);
        Notifications.Insert(0, n);
        while (Notifications.Count > 5) Notifications.RemoveAt(Notifications.Count - 1);
        if (level == NotificationLevel.Info)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            t.Tick += (_, _) => { t.Stop(); Notifications.Remove(n); };
            t.Start();
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
    }
}
