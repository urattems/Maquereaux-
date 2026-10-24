using System.Collections.Concurrent;
using Maquereaux.Core;
using Maquereaux.Core.Activation;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Profiles;
using Maquereaux.Core.Stats;
using Maquereaux.Win32;
using Microsoft.Extensions.Logging;

namespace Maquereaux.App.Services;

/// <summary>
/// « Racine de composition » : crée et relie tous les services (moteur, hotkeys, boucle Win32…).
/// Aucune logique d'UI ici ; les ViewModels parlent à cette classe.
/// </summary>
public sealed class AppHost : IDisposable
{
    private const int EmergencyHotkeyId = 1;
    private const int EmergencyAltHotkeyId = 2;
    private const int ProfileHotkeyIdBase = 100;

    private readonly ILogger _log;
    private readonly MessageLoopThread _loop;
    private readonly GlobalHotkeyManager _hotkeys;
    private readonly SerialExecutor _executor;
    private readonly ActivationController _controller;
    private volatile IReadOnlyDictionary<int, Guid> _hotkeyToProfile = new Dictionary<int, Guid>();
    private AppSettings _settings = new();
    private bool _profileHotkeysSuspended;
    private volatile TimerStrategy _timerStrategy = TimerStrategy.HighResolution;
    private EngineState _lastState = EngineState.Stopped;
    private bool _disposed;

    public AppHost(AppPaths paths, ILoggerFactory loggerFactory)
    {
        Paths = paths;
        LoggerFactory = loggerFactory;
        _log = loggerFactory.CreateLogger<AppHost>();

        Sender = new SendInputSender();
        Engine = new MacroEngine(Sender, () => SleeperFactory.Create(_timerStrategy, StopwatchClock.Instance), StopwatchClock.Instance)
        {
            Observer = Recorder,
        };
        Engine.StateChanged += OnEngineStateChanged;

        _executor = new SerialExecutor();
        _executor.ActionFailed += ex => _log.LogError(ex, "Commande en échec");

        _controller = new ActivationController(Engine, Profiles.Find, CreateGate);
        _controller.Error += msg => { _log.LogWarning("{Message}", msg); Error?.Invoke(msg); };
        _controller.ProfileActivated += id => ProfileActivated?.Invoke(id);

        _loop = new MessageLoopThread();
        _hotkeys = new GlobalHotkeyManager(_loop);
        _hotkeys.Pressed += OnHotkeyPressed;

        _log.LogInformation("Maquereaux {Version} démarré (données : {Dir})", AppInfo.Version, paths.DataDir);
    }

    public AppPaths Paths { get; }
    public ILoggerFactory LoggerFactory { get; }
    public MacroEngine Engine { get; }
    public SendInputSender Sender { get; }

    /// <summary>Intervalles réels mesurés pendant l'exécution (stats en direct).</summary>
    public TimingRecorder Recorder { get; } = new();

    /// <summary>Stratégie d'attente utilisée au prochain démarrage.</summary>
    public TimerStrategy TimerStrategy
    {
        get => _timerStrategy;
        set => _timerStrategy = value;
    }

    /// <summary>Copies thread-safe des profils (publiées par l'UI à chaque modification).</summary>
    public ProfileRegistry Profiles { get; } = new();

    /// <summary>Changement d'état du moteur (levé hors thread UI !).</summary>
    public event EventHandler<EngineStateChangedEventArgs>? EngineStateChanged;

    /// <summary>Erreur à afficher à l'utilisateur (levé hors thread UI !).</summary>
    public event Action<string>? Error;

    /// <summary>Un profil vient d'être lancé par son raccourci (levé hors thread UI !).</summary>
    public event Action<Guid>? ProfileActivated;

    // ---------- Commandes ----------

    public void Toggle(Guid profileId) => _executor.Post(() => _controller.Toggle(profileId));

    public void Stop() => _executor.Post(_controller.Stop);

    public void EmergencyStop()
    {
        // Double sécurité : annulation immédiate (non bloquante) + arrêt complet via la file de commandes.
        Engine.RequestStop(StopReason.Emergency);
        _executor.Post(_controller.EmergencyStop);
    }

    // ---------- Hotkeys ----------

    /// <summary>
    /// (Ré)enregistre tous les raccourcis : arrêt d'urgence + un par profil.
    /// Retourne les messages d'erreur (conflits) à afficher.
    /// </summary>
    public IReadOnlyList<string> ApplyHotkeys(AppSettings settings)
    {
        _settings = settings.Clone();
        var errors = new List<string>();
        _hotkeys.UnregisterAll();

        var emergency = _settings.EmergencyStopHotkey;
        var r = _hotkeys.Register(EmergencyHotkeyId, emergency);
        if (!r.Success) errors.Add("Arrêt d'urgence — " + r.Describe(emergency));
        // Piège : avec Ctrl enfoncé, la touche Pause envoie VK_CANCEL (« Break ») au lieu de VK_PAUSE.
        // On enregistre donc les deux variantes pour que Ctrl+Alt+Pause marche sur tous les claviers.
        if (emergency.VirtualKey == VirtualKeys.Pause && emergency.Modifiers.HasFlag(HotkeyModifiers.Control))
            _hotkeys.Register(EmergencyAltHotkeyId, emergency with { VirtualKey = VirtualKeys.Cancel });

        if (!_profileHotkeysSuspended) errors.AddRange(RegisterProfileHotkeys());
        foreach (var e in errors) _log.LogWarning("Hotkey : {Error}", e);
        return errors;
    }

    private List<string> RegisterProfileHotkeys()
    {
        var errors = new List<string>();
        var map = new Dictionary<int, Guid>();
        int id = ProfileHotkeyIdBase;
        foreach (var p in Profiles.All.OrderBy(p => p.Name))
        {
            if (p.Hotkey is null) continue;
            if (p.Hotkey == _settings.EmergencyStopHotkey)
            {
                errors.Add($"« {p.Name} » : {p.Hotkey} est déjà l'arrêt d'urgence.");
                continue;
            }
            var res = _hotkeys.Register(id, p.Hotkey);
            if (res.Success) map[id] = p.Id;
            else errors.Add($"« {p.Name} » — {res.Describe(p.Hotkey)}");
            id++;
        }
        _hotkeyToProfile = map;
        return errors;
    }

    /// <summary>
    /// Suspend les raccourcis des profils pendant qu'on capture une touche dans l'UI
    /// (sinon appuyer sur F6 pour la capturer lancerait la macro). L'arrêt d'urgence reste actif.
    /// </summary>
    public void SuspendProfileHotkeys(bool suspend)
    {
        if (_profileHotkeysSuspended == suspend || _disposed) return;
        _profileHotkeysSuspended = suspend;
        if (suspend)
        {
            foreach (var id in _hotkeyToProfile.Keys) _hotkeys.Unregister(id);
            _hotkeyToProfile = new Dictionary<int, Guid>();
        }
        else
        {
            RegisterProfileHotkeys();
        }
    }

    private void OnHotkeyPressed(int id)
    {
        // Thread de la boucle Win32 : on ne fait que poster, jamais attendre.
        if (id is EmergencyHotkeyId or EmergencyAltHotkeyId)
        {
            _log.LogInformation("Arrêt d'urgence (hotkey)");
            EmergencyStop();
        }
        else if (_hotkeyToProfile.TryGetValue(id, out var profileId))
        {
            Toggle(profileId);
        }
    }

    // ---------- Divers ----------

    private ISendGate? CreateGate(Profile profile) => null;

    private void OnEngineStateChanged(object? sender, EngineStateChangedEventArgs e)
    {
        // Nouvelle exécution : on repart de stats vierges (levé AVANT le premier tick)
        if (e.State == EngineState.Running && _lastState == EngineState.Stopped) Recorder.Reset();
        _lastState = e.State;

        if (e.State == EngineState.Stopped)
        {
            if (e.Error is not null) _log.LogError(e.Error, "Moteur arrêté sur erreur");
            else _log.LogInformation("Moteur arrêté ({Reason}), {Ticks} ticks", e.Reason, Engine.TicksSent);
        }
        else
        {
            _log.LogInformation("Moteur : {State} « {Profile} »", e.State, e.Profile?.Name);
        }
        EngineStateChanged?.Invoke(this, e);
    }

    /// <summary>Dernier recours en cas de crash : relâche toute touche enfoncée.</summary>
    public void PanicReleaseKeys()
    {
        try
        {
            Engine.RequestStop(StopReason.Error);
            Engine.ReleaseAllKeys();
        }
        catch { /* on fait au mieux */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _log.LogInformation("Fermeture");
        Engine.Stop(StopReason.Shutdown); // relâche les touches avant tout le reste
        _hotkeys.Dispose();
        _loop.Dispose();
        _executor.Dispose();
    }
}
