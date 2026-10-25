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
        _isSidebarCollapsed = _settings.SidebarCollapsed;
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

        // Rafraîchissement des compteurs à 10 Hz (plafond fixé pour ne jamais concurrencer le moteur) :
        // le moteur ne pousse rien vers l'UI à chaque tick, c'est l'UI qui vient lire un instantané.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
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
    public string Version => "v" + AppInfo.Version;

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
    [NotifyPropertyChangedFor(nameof(StateCaption))]
    [NotifyPropertyChangedFor(nameof(HeroTicks))]
    private EngineState _engineState = EngineState.Stopped;

    [ObservableProperty]
    private string? _runningProfileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroTicks))]
    private long _ticksSent;

    [ObservableProperty]
    private string _lastStopInfo = "Prêt.";

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private Hotkey _emergencyHotkey;

    /// <summary>Stats de timing en direct (rafraîchies 10×/s pendant l'exécution).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroMean))]
    [NotifyPropertyChangedFor(nameof(HeroJitter))]
    [NotifyPropertyChangedFor(nameof(HeroP95))]
    private TimingStats _liveStats = TimingStats.Empty;

    /// <summary>Derniers intervalles mesurés (mini courbe de la carte héros).</summary>
    [ObservableProperty]
    private IReadOnlyList<double> _recentIntervals = Array.Empty<double>();

    /// <summary>Appuis par seconde réellement envoyés (Burst : toute la séquence par tick).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroRate))]
    private double _liveInputsPerSecond;

    // Valeurs formatées de la carte héros (« — » tant qu'aucune mesure n'existe)
    public string HeroTicks => TicksSent == 0 && !IsRunning ? "—" : TicksSent.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
    public string HeroRate => LiveInputsPerSecond <= 0 ? "—" : LiveInputsPerSecond.ToString(LiveInputsPerSecond >= 100 ? "0" : "0.0", System.Globalization.CultureInfo.CurrentCulture);
    public string HeroMean => LiveStats.IsEmpty ? "—" : LiveStats.Mean.ToString("0.000", System.Globalization.CultureInfo.CurrentCulture);
    public string HeroJitter => LiveStats.IsEmpty ? "—" : LiveStats.StdDev.ToString("0.000", System.Globalization.CultureInfo.CurrentCulture);
    public string HeroP95 => LiveStats.IsEmpty ? "—" : LiveStats.P95.ToString("0.000", System.Globalization.CultureInfo.CurrentCulture);

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

    // ---------- Navigation (coque v1.1) ----------

    /// <summary>Pages de la navigation latérale (même ordre que les onglets de la fenêtre).</summary>
    public IReadOnlyList<NavItem> NavItems { get; } = new[]
    {
        new NavItem("Macro", "Compose ta séquence, règle la cadence et lance-la.", "Icon.Keyboard", "Ctrl+1"),
        new NavItem("Profils", "Une configuration par usage, chacune avec son raccourci.", "Icon.Profiles", "Ctrl+2"),
        new NavItem("Labo", "Mesure ton timing et compare manuel contre automatique.", "Icon.Lab", "Ctrl+3"),
        new NavItem("Paramètres", "Raccourcis, précision du timer, sécurité et apparence.", "Icon.Settings", "Ctrl+4"),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int _selectedPageIndex;

    public NavItem CurrentPage => NavItems[Math.Clamp(SelectedPageIndex, 0, NavItems.Count - 1)];

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        _settings.SidebarCollapsed = value;
        MarkDirty();
    }

    /// <summary>Ctrl+Z hors champ texte : annule la dernière suppression de touche du profil actif.</summary>
    [RelayCommand]
    private void UndoStepRemoval()
    {
        if (ActiveProfile is { CanUndoRemove: true } p) p.UndoRemoveStepCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void ToggleTheme() => IsLightTheme = !IsLightTheme;

    [RelayCommand]
    private void Navigate(string? page)
    {
        var i = NavItems.ToList().FindIndex(n => n.Label == page);
        if (i >= 0) SelectedPageIndex = i;
    }

    /// <summary>Description courte de l'état pour la pastille globale (ex. « Profil 1 · F6 »).</summary>
    public string StateCaption => EngineState switch
    {
        EngineState.Running => "En marche",
        EngineState.Paused => "En pause",
        _ => "À l'arrêt",
    };

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
        // Pas de boîte de confirmation : suppression immédiate + toast « Annuler » (plus rapide, et rien n'est perdu)
        if (_host.Engine.RunningProfile?.Id == p.Id) _host.Stop();
        int index = Profiles.IndexOf(p);
        // La liste liée (SelectedItem) remet ActiveProfile à null pendant le Remove : on décide avant
        bool wasActive = ActiveProfile == p;
        Profiles.Remove(p);
        _host.Profiles.Remove(p.Id);
        if (wasActive || ActiveProfile is null) ActiveProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
        ProfilesStructureChanged();

        NotificationViewModel? toast = null;
        toast = Notify($"Profil « {p.Name} » supprimé.", NotificationLevel.Info, "Annuler", new RelayCommand(() =>
        {
            RestoreProfile(p, index);
            if (toast is not null) Notifications.Remove(toast);
        }));
    }

    /// <summary>Annule une suppression : le profil revient à sa place (sans son raccourci s'il a été repris entre-temps).</summary>
    private void RestoreProfile(ProfileViewModel p, int index)
    {
        if (Profiles.Contains(p)) return;
        if (p.Hotkey is { } hk && (hk == _settings.EmergencyStopHotkey || Profiles.Any(o => o.Hotkey == hk)))
            p.Hotkey = null;
        Profiles.Insert(Math.Clamp(index, 0, Profiles.Count), p);
        _host.Profiles.Publish(p.Model);
        ActiveProfile = p;
        ProfilesStructureChanged();
    }

    /// <summary>Rend le profil actif et ouvre l'écran Macro pour l'éditer.</summary>
    [RelayCommand]
    private void EditProfile(ProfileViewModel? p)
    {
        if (p is null) return;
        ActiveProfile = p;
        Navigate("Macro");
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
        if (value is null || !value.IsValid || value.IsMouse)
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
        var samples = _host.Recorder.GetSamples();
        LiveStats = Core.Stats.TimingStats.Compute(samples);
        // Mini courbe : les 120 derniers intervalles
        RecentIntervals = samples.Skip(Math.Max(0, samples.Length - 120)).Select(x => x.Actual).ToArray();
        var running = _host.Engine.RunningProfile;
        LiveInputsPerSecond = running is null || LiveStats.IsEmpty ? 0
            : LiveStats.RateHz * (running.SequenceMode == SequenceMode.Burst ? Math.Max(1, running.Steps.Count) : 1);
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

    public void Notify(string message, NotificationLevel level = NotificationLevel.Info) => Notify(message, level, null, null);

    /// <summary>
    /// Affiche un toast. Info/Success disparaissent seuls (6 s, 8 s s'il y a une action) ; Warning/Error restent jusqu'à fermeture.
    /// Retourne le toast (permet de le retirer, ex. après « Annuler »).
    /// </summary>
    public NotificationViewModel? Notify(string message, NotificationLevel level, string? actionLabel, System.Windows.Input.ICommand? action)
    {
        // Pas de doublon : le même message déjà affiché n'est pas répété
        if (Notifications.Any(n => n.Message == message)) return null;
        var n = new NotificationViewModel(message, level, actionLabel, action);
        Notifications.Add(n); // le plus récent en bas, au plus près de l'œil
        while (Notifications.Count > 4) Notifications.RemoveAt(0);
        if (level is NotificationLevel.Info or NotificationLevel.Success)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(actionLabel is null ? 6 : 8) };
            t.Tick += (_, _) => { t.Stop(); Notifications.Remove(n); };
            t.Start();
        }
        return n;
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        if (_saveTimer.IsEnabled) { _saveTimer.Stop(); SaveNow(); }
        Lab.Dispose();
    }
}
