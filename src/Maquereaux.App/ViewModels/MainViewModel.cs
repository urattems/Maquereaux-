using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maquereaux.App.Services;
using Maquereaux.Core;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Models;
using Maquereaux.Core.Profiles;
using Maquereaux.Core.Stats;
using Maquereaux.Win32;

namespace Maquereaux.App.ViewModels;

/// <summary>ViewModel principal : état du moteur, profils, réglages, notifications.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppHost _host;
    private readonly Dispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private readonly ProfileRepository _repo;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly AppSettings _settings;
    private readonly HashSet<string> _uipiWarned = new(StringComparer.OrdinalIgnoreCase);
    private bool _loading;

    public MainViewModel(AppHost host, Dispatcher dispatcher, IDialogService dialogs)
    {
        _host = host;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _repo = new ProfileRepository(host.Paths);

        // ----- Chargement (profils + réglages) -----
        _loading = true;
        _settings = _repo.LoadSettings();
        foreach (var p in _repo.LoadProfiles()) AddProfileVm(p);
        _activeProfile = Profiles.FirstOrDefault(p => p.Id == _settings.ActiveProfileId) ?? Profiles[0];
        _activeProfile.IsActive = true;
        _alwaysOnTop = _settings.AlwaysOnTop;
        _emergencyHotkey = _settings.EmergencyStopHotkey;
        _timerStrategy = _settings.TimerStrategy;
        _isLightTheme = _settings.Theme == AppTheme.Light;
        _safetyTimeoutMinutes = _settings.SafetyTimeoutMinutes;
        _minimizeToTray = _settings.MinimizeToTray;
        _host.TimerStrategy = _timerStrategy;
        _host.Engine.MaxRunDuration = TimeSpan.FromMinutes(_safetyTimeoutMinutes);
        _loading = false;

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

        // Sauvegarde automatique « anti-rebond » : 600 ms après la dernière modification.
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };

        _liveEditTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
        _liveEditTimer.Tick += (_, _) =>
        {
            _liveEditTimer.Stop();
            if (_liveEditProfile is { } id) _host.ApplyLiveEdit(id);
        };

        Lab = new LabViewModel(host, dispatcher, dialogs, this);
    }

    private readonly DispatcherTimer _liveEditTimer;
    private Guid? _liveEditProfile;

    /// <summary>Position/taille de fenêtre mémorisées (lues/écrites par la vue).</summary>
    public WindowPlacement? WindowPlacement
    {
        get => _settings.Window;
        set { _settings.Window = value; MarkDirty(); }
    }

    public string Title => $"Maquereaux {AppInfo.Version}";

    public ObservableCollection<ProfileViewModel> Profiles { get; } = new();
    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();
    public ObservableCollection<string> RunningProcesses { get; } = new();

    /// <summary>Onglet Labo (J4).</summary>
    public LabViewModel Lab { get; }

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

    [ObservableProperty]
    private bool _isLightTheme;

    [ObservableProperty]
    private int _safetyTimeoutMinutes;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _isCompact;

    public bool IsHybridTimer => TimerStrategy == TimerStrategy.Hybrid;
    public string TimerStrategyDescription => SleeperFactory.Describe(TimerStrategy);
    public bool IsElevated => ProcessInfo.IsCurrentProcessElevated;
    public string DataDir => _host.Paths.DataDir;

    public bool IsRunning => EngineState != EngineState.Stopped;
    public string StartStopLabel => IsRunning ? "■  STOP" : "▶  START";

    /// <summary>Thème demandé (appliqué par la vue).</summary>
    public event Action<bool>? ThemeChanged;

    /// <summary>À appeler une fois la fenêtre affichée : publie les profils, enregistre les hotkeys, affiche les alertes de chargement.</summary>
    public void Initialize()
    {
        foreach (var p in Profiles) _host.Profiles.Publish(p.Model);
        ApplyHotkeys();
        RevalidateAll();
        foreach (var w in _repo.Warnings) Notify(w, NotificationLevel.Warning);
        RefreshProcesses();
    }

    // ---------- Commandes moteur ----------

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

    [RelayCommand]
    private void ToggleCompact() => IsCompact = !IsCompact;

    // ---------- Commandes profils ----------

    [RelayCommand]
    private void NewProfile()
    {
        var p = new Profile { Name = UniqueName("Nouveau profil"), Hotkey = null };
        p.Steps.Add(new MacroStep(KeyStroke.FromVirtualKey(Core.Input.VirtualKeys.A)));
        ActiveProfile = AddProfileVm(p, publish: true);
        ProfilesStructureChanged();
    }

    [RelayCommand]
    private void DuplicateProfile(ProfileViewModel? source)
    {
        source ??= ActiveProfile;
        if (source is null) return;
        var copy = source.Model.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name = UniqueName(source.Name + " (copie)");
        copy.Hotkey = null; // deux profils ne peuvent pas partager un raccourci
        ActiveProfile = AddProfileVm(copy, publish: true);
        ProfilesStructureChanged();
    }

    [RelayCommand]
    private void DeleteProfile(ProfileViewModel? p)
    {
        p ??= ActiveProfile;
        if (p is null) return;
        if (Profiles.Count == 1)
        {
            Notify("Impossible de supprimer le dernier profil.", NotificationLevel.Warning);
            return;
        }
        if (!_dialogs.Confirm("Supprimer le profil", $"Supprimer définitivement « {p.Name} » ?")) return;
        if (_host.Engine.RunningProfile?.Id == p.Id) _host.Stop();
        int index = Profiles.IndexOf(p);
        Profiles.Remove(p);
        _host.Profiles.Remove(p.Id);
        if (ActiveProfile == p) ActiveProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
        ProfilesStructureChanged();
    }

    [RelayCommand]
    private void SelectProfile(ProfileViewModel? p)
    {
        if (p is not null) ActiveProfile = p;
    }

    [RelayCommand]
    private void ImportProfiles()
    {
        var path = _dialogs.OpenFile("Importer des profils", DialogService.JsonFilter);
        if (path is null) return;
        try
        {
            var imported = ProfileRepository.Import(path, Profiles.Select(p => p.Id));
            foreach (var p in imported)
            {
                p.Name = UniqueName(p.Name);
                // Raccourci déjà utilisé ailleurs → on le retire plutôt que de créer un conflit
                if (p.Hotkey is not null && (p.Hotkey == _settings.EmergencyStopHotkey || Profiles.Any(o => o.Hotkey == p.Hotkey)))
                    p.Hotkey = null;
                AddProfileVm(p, publish: true);
            }
            ProfilesStructureChanged();
            Notify($"{imported.Count} profil(s) importé(s).");
        }
        catch (InvalidDataException ex)
        {
            Notify("Import impossible : " + ex.Message, NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private void ExportProfile(ProfileViewModel? p)
    {
        p ??= ActiveProfile;
        if (p is null) return;
        var path = _dialogs.SaveFile("Exporter le profil", DialogService.JsonFilter, SafeFileName(p.Name) + ".json");
        if (path is null) return;
        Export(path, new[] { p.Model });
    }

    [RelayCommand]
    private void ExportAllProfiles()
    {
        var path = _dialogs.SaveFile("Exporter tous les profils", DialogService.JsonFilter, "maquereaux-profils.json");
        if (path is null) return;
        Export(path, Profiles.Select(p => p.Model));
    }

    [RelayCommand]
    private void RefreshProcesses()
    {
        var current = ActiveProfile?.TargetProcess;
        RunningProcesses.Clear();
        foreach (var n in ProcessInfo.GetWindowedProcessNames()) RunningProcesses.Add(n);
        if (current is not null && !RunningProcesses.Contains(current)) RunningProcesses.Insert(0, current);
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_host.Paths.DataDir}\"") { UseShellExecute = true }); }
        catch (Exception ex) { Notify("Impossible d'ouvrir le dossier : " + ex.Message, NotificationLevel.Warning); }
    }

    [RelayCommand]
    private void RestartAsAdmin()
    {
        if (IsElevated) return;
        if (!_dialogs.Confirm("Relancer en administrateur",
                "Nécessaire seulement si l'appli cible tourne elle-même en administrateur (sinon Windows bloque les touches envoyées : protection UIPI).\n\nRelancer Maquereaux en administrateur ?"))
            return;
        try
        {
            var exe = Environment.ProcessPath!;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            SaveNow();
            System.Windows.Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Notify("Relance annulée.", NotificationLevel.Info);
        }
    }

    // ---------- Réglages ----------

    partial void OnActiveProfileChanged(ProfileViewModel? oldValue, ProfileViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is null) return;
        newValue.IsActive = true;
        _settings.ActiveProfileId = newValue.Id;
        if (newValue.TargetProcess is { } t && !RunningProcesses.Contains(t)) RunningProcesses.Insert(0, t);
        MarkDirty();
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        _settings.AlwaysOnTop = value;
        MarkDirty();
    }

    partial void OnTimerStrategyChanged(TimerStrategy value)
    {
        _settings.TimerStrategy = value;
        _host.TimerStrategy = value;
        if (IsRunning) Notify("Nouvelle stratégie de timer : prise en compte au prochain démarrage.");
        MarkDirty();
    }

    partial void OnIsLightThemeChanged(bool value)
    {
        _settings.Theme = value ? AppTheme.Light : AppTheme.Dark;
        ThemeChanged?.Invoke(value);
        MarkDirty();
    }

    partial void OnSafetyTimeoutMinutesChanged(int value)
    {
        var v = Math.Clamp(value, AppSettings.MinSafetyTimeoutMinutes, AppSettings.MaxSafetyTimeoutMinutes);
        if (v != value) { SafetyTimeoutMinutes = v; return; }
        _settings.SafetyTimeoutMinutes = v;
        _host.Engine.MaxRunDuration = TimeSpan.FromMinutes(v);
        MarkDirty();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settings.MinimizeToTray = value;
        MarkDirty();
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
        RevalidateAll();
        MarkDirty();
    }

    // ---------- Interne ----------

    private ProfileViewModel AddProfileVm(Profile model, bool publish = false)
    {
        var vm = new ProfileViewModel(model);
        vm.Changed += OnProfileChanged;
        Profiles.Add(vm);
        if (publish) _host.Profiles.Publish(model);
        return vm;
    }

    private void OnProfileChanged(ProfileViewModel vm, bool hotkeysAffected)
    {
        _host.Profiles.Publish(vm.Model);
        if (hotkeysAffected) ApplyHotkeys();
        // Modification du profil en cours → appliquée à chaud (anti-rebond pour ne pas relancer à chaque frappe)
        if (vm.IsRunning)
        {
            _liveEditTimer.Stop();
            _liveEditProfile = vm.Id;
            _liveEditTimer.Start();
        }
        RevalidateAll();
        MarkDirty();
    }

    private void ProfilesStructureChanged()
    {
        ApplyHotkeys();
        RevalidateAll();
        MarkDirty();
    }

    private void RevalidateAll()
    {
        var models = Profiles.Select(p => p.Model).ToList();
        foreach (var p in Profiles) p.Issues = ProfileValidator.Validate(p.Model, models, _settings);
    }

    private void ApplyHotkeys()
    {
        var errors = _host.ApplyHotkeys(_settings);
        foreach (var e in errors) Notify(e, NotificationLevel.Warning);
    }

    private void MarkDirty()
    {
        if (_loading) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Sauvegarde immédiate (écriture atomique). Appelée aussi à la fermeture.</summary>
    public void SaveNow()
    {
        try
        {
            _repo.SaveProfiles(Profiles.Select(p => p.Model));
            _repo.SaveSettings(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify("Sauvegarde impossible : " + ex.Message, NotificationLevel.Error);
        }
    }

    private void Export(string path, IEnumerable<Profile> profiles)
    {
        try
        {
            ProfileRepository.Export(path, profiles);
            Notify($"Exporté : {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify("Export impossible : " + ex.Message, NotificationLevel.Error);
        }
    }

    private string UniqueName(string baseName)
    {
        var name = baseName.Length > ProfileLimits.MaxNameLength - 4 ? baseName[..(ProfileLimits.MaxNameLength - 4)] : baseName;
        if (Profiles.All(p => p.Name != name)) return name;
        for (int i = 2; ; i++)
        {
            var candidate = $"{name} {i}";
            if (Profiles.All(p => p.Name != candidate)) return candidate;
        }
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private void OnEngineStateChanged(EngineStateChangedEventArgs e)
    {
        EngineState = _host.Engine.State;
        RunningProfileName = e.State == EngineState.Stopped ? null : e.Profile?.Name;
        var runningId = _host.Engine.RunningProfile?.Id;
        foreach (var p in Profiles) p.IsRunning = IsRunning && p.Id == runningId;
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
                StopReason.SafetyTimeout => $"Arrêt de sécurité : {SafetyTimeoutMinutes} min atteintes (réglable dans Paramètres).",
                StopReason.HoldReleased => "Touche relâchée.",
                StopReason.Replaced => LastStopInfo,
                _ => "Arrêté.",
            };
            if (e.Reason is StopReason.Error or StopReason.SafetyTimeout) Notify(LastStopInfo, NotificationLevel.Warning);
            if (_host.Sender.FailedEvents > 0)
                Notify($"{_host.Sender.FailedEvents} événement(s) refusé(s) par Windows (fenêtre admin ? bureau sécurisé ?).", NotificationLevel.Warning);
        }
        else if (e.State == EngineState.Running)
        {
            LastStopInfo = e.Profile?.TargetProcess is { } t
                ? $"En cours : « {e.Profile.Name} » (uniquement dans {t})"
                : $"En cours : « {e.Profile?.Name} »";
        }
        else if (e.State == EngineState.Paused)
        {
            LastStopInfo = $"En pause : {e.Profile?.TargetProcess} n'a pas le focus.";
        }
    }

    private void RefreshLiveData()
    {
        TicksSent = _host.Engine.TicksSent;
        LiveStats = _host.Recorder.Snapshot();
        ActiveTimerName = _host.Engine.CurrentSleeperName ?? ActiveTimerName;
        CheckUipi();
    }

    /// <summary>
    /// UIPI : si la fenêtre au premier plan tourne en admin et pas nous, Windows jette nos touches sans rien dire.
    /// On prévient l'utilisateur (une fois par appli).
    /// </summary>
    private void CheckUipi()
    {
        if (EngineState != EngineState.Running) return;
        var blocked = ProcessInfo.DetectUipiBlockedForeground();
        if (blocked is null || !_uipiWarned.Add(blocked)) return;
        Notify($"« {blocked} » tourne en administrateur : Windows bloque les touches envoyées par Maquereaux (protection UIPI). " +
               "Relance Maquereaux en administrateur (Paramètres) si tu en as besoin.", NotificationLevel.Warning);
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
        if (_saveTimer.IsEnabled) { _saveTimer.Stop(); SaveNow(); }
        Lab.Dispose();
    }
}
