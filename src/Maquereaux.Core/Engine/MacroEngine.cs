using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Engine;

public enum EngineState
{
    Stopped,
    Running,
    /// <summary>En cours mais en pause (ex. la fenêtre cible n'a plus le focus).</summary>
    Paused,
}

public enum StopReason
{
    None,
    /// <summary>Arrêt demandé (bouton, hotkey).</summary>
    Manual,
    /// <summary>RepeatCount atteint.</summary>
    Completed,
    /// <summary>Arrêt d'urgence.</summary>
    Emergency,
    /// <summary>Exception pendant l'envoi.</summary>
    Error,
    /// <summary>Durée maximale de sécurité atteinte.</summary>
    SafetyTimeout,
    /// <summary>Fermeture de l'appli.</summary>
    Shutdown,
    /// <summary>Un autre profil a été lancé à la place.</summary>
    Replaced,
    /// <summary>Touche de maintien relâchée (mode Hold).</summary>
    HoldReleased,
}

public sealed class EngineStateChangedEventArgs : EventArgs
{
    public EngineStateChangedEventArgs(EngineState state, Profile? profile, StopReason reason, Exception? error)
    {
        State = state;
        Profile = profile;
        Reason = reason;
        Error = error;
    }

    public EngineState State { get; }
    public Profile? Profile { get; }
    public StopReason Reason { get; }
    public Exception? Error { get; }
}

/// <summary>Reçoit l'horodatage de chaque tick (sert aux statistiques de timing en direct).</summary>
public interface ITickObserver
{
    /// <summary>Début d'une exécution (ou reprise après pause) : l'intervalle précédent n'a plus de sens.</summary>
    void OnResync();

    /// <summary>Un tick vient d'être envoyé à <paramref name="nowMs"/> ; il était prévu <paramref name="expectedIntervalMs"/> après le précédent.</summary>
    void OnTick(double nowMs, double expectedIntervalMs);
}

/// <summary>
/// Le cœur de Maquereaux : joue un <see cref="Profile"/> en boucle sur un thread dédié.
/// <list type="bullet">
/// <item>Thread dédié (jamais le thread UI) en priorité AboveNormal : l'UI peut ramer, le timing non.</item>
/// <item>Cadence ABSOLUE : on vise t0 + Σ délais ; un réveil en retard ne décale pas les ticks suivants.</item>
/// <item>Anti « touche bloquée » : toute touche enfoncée est relâchée dans un <c>finally</c>, quoi qu'il arrive.</item>
/// </list>
/// </summary>
public sealed class MacroEngine : IMacroRunner, IDisposable
{
    private readonly IInputSender _sender;
    private readonly IClock _clock;
    private readonly object _lifecycleLock = new();
    private readonly object _keysLock = new();
    private readonly Dictionary<ushort, KeyStroke> _pressed = new();
    private Func<ISleeper> _sleeperFactory;
    private RunContext? _current;
    private volatile EngineState _state = EngineState.Stopped;
    private long _ticksSent;

    /// <param name="sender">Envoi des touches.</param>
    /// <param name="sleeperFactory">
    /// Crée la stratégie d'attente. Appelée à CHAQUE démarrage, et le sleeper est disposé à l'arrêt
    /// (dans un finally) : une ressource système comme timeBeginPeriod(1) est donc toujours rendue.
    /// </param>
    /// <param name="clock">Horloge de référence.</param>
    public MacroEngine(IInputSender sender, Func<ISleeper> sleeperFactory, IClock clock)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _sleeperFactory = sleeperFactory ?? throw new ArgumentNullException(nameof(sleeperFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Levé à chaque changement d'état, depuis le thread appelant ou le thread moteur.</summary>
    public event EventHandler<EngineStateChangedEventArgs>? StateChanged;

    public EngineState State => _state;
    public bool IsRunning => _state != EngineState.Stopped;

    /// <summary>Instantané du profil en cours d'exécution (null si arrêté).</summary>
    public Profile? RunningProfile => _current?.Profile;

    /// <summary>Nombre de ticks envoyés depuis le dernier démarrage.</summary>
    public long TicksSent => Interlocked.Read(ref _ticksSent);

    /// <summary>Observateur de timing (stats en direct). Optionnel.</summary>
    public ITickObserver? Observer { get; set; }

    /// <summary>Durée max d'une exécution (garde-fou anti-oubli). null = pas de limite.</summary>
    public TimeSpan? MaxRunDuration { get; set; }

    /// <summary>Fabrique de la stratégie d'attente. Changeable à chaud : prise en compte au prochain démarrage.</summary>
    public Func<ISleeper> SleeperFactory
    {
        get => _sleeperFactory;
        set => _sleeperFactory = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Nom de la stratégie d'attente de l'exécution en cours (null si arrêté).</summary>
    public string? CurrentSleeperName => _current?.SleeperName;

    /// <summary>Lance le profil (arrête d'abord l'exécution en cours s'il y en a une).</summary>
    /// <exception cref="ArgumentException">Séquence vide.</exception>
    public void Start(Profile profile, ISendGate? gate = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var snapshot = profile.Clone();
        var plan = MacroPlan.Build(snapshot); // valide avant de toucher à l'exécution en cours

        lock (_lifecycleLock)
        {
            StopCore(StopReason.Replaced);

            _sender.Prepare(plan.DistinctKeys);
            var ctx = new RunContext(snapshot, plan, gate ?? OpenGate.Instance, _sleeperFactory, MaxRunDuration);
            Interlocked.Exchange(ref _ticksSent, 0);
            _current = ctx;
            _state = EngineState.Running;
            RaiseStateChanged(new EngineStateChangedEventArgs(EngineState.Running, snapshot, StopReason.None, null));

            ctx.Thread = new Thread(() => Run(ctx))
            {
                Name = "Maquereaux.Engine",
                IsBackground = true,
                // AboveNormal (pas Highest) : le moteur passe devant l'UI sans affamer le reste du système.
                Priority = ThreadPriority.AboveNormal,
            };
            ctx.Thread.Start();
        }
    }

    /// <summary>Arrête l'exécution en cours et attend la fin du thread moteur (≤ 2 s).</summary>
    public void Stop(StopReason reason = StopReason.Manual)
    {
        lock (_lifecycleLock) StopCore(reason);
    }

    /// <summary>
    /// Demande d'arrêt NON bloquante (n'attend pas la fin du thread). Utilisée par l'arrêt d'urgence
    /// depuis le thread des hotkeys, qui ne doit jamais attendre.
    /// </summary>
    public void RequestStop(StopReason reason)
    {
        var ctx = _current;
        if (ctx is null) return;
        ctx.RequestedReason = reason;
        try { ctx.Cts.Cancel(); }
        catch (ObjectDisposedException) { /* déjà terminé */ }
    }

    private void StopCore(StopReason reason)
    {
        var ctx = _current;
        if (ctx is null) return;
        ctx.RequestedReason = reason;
        ctx.Cts.Cancel();
        // Si Stop est appelé depuis le thread moteur lui-même (ex. dans StateChanged), on ne peut pas s'attendre soi-même.
        if (ctx.Thread is not null && Thread.CurrentThread != ctx.Thread)
            ctx.Thread.Join(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Relâche immédiatement toute touche encore enfoncée par le moteur. Thread-safe.
    /// Appelé en dernier recours (exception non gérée, fermeture brutale).
    /// </summary>
    public void ReleaseAllKeys()
    {
        lock (_keysLock)
        {
            if (_pressed.Count == 0) return;
            var ups = _pressed.Values.Select(KeyAction.Up).ToArray();
            _pressed.Clear();
            _sender.Send(ups);
        }
    }

    private void Run(RunContext ctx)
    {
        var ct = ctx.Cts.Token;
        var stopReason = StopReason.Completed;
        Exception? error = null;
        ISleeper? sleeper = null;
        try
        {
            sleeper = ctx.SleeperFactory();
            ctx.SleeperName = sleeper.Name;
            stopReason = Loop(ctx, sleeper, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopReason = ctx.RequestedReason;
        }
        catch (Exception ex)
        {
            error = ex;
            stopReason = StopReason.Error;
        }
        finally
        {
            try { ReleaseAllKeys(); }
            catch (Exception ex) { error ??= ex; }
            // Toujours libérer le sleeper (ex. timeEndPeriod), même après une exception
            try { sleeper?.Dispose(); }
            catch (Exception ex) { error ??= ex; }
        }

        // Fin d'exécution : on ne nettoie _current que s'il s'agit toujours de NOTRE exécution
        // (un Start concurrent a pu le remplacer). Pas de lock ici : Stop() tient le lock en attendant ce thread.
        if (Interlocked.CompareExchange(ref _current, null, ctx) == ctx)
            _state = EngineState.Stopped;
        RaiseStateChanged(new EngineStateChangedEventArgs(EngineState.Stopped, ctx.Profile, stopReason, error));
        // Note : la CancellationTokenSource n'est volontairement pas Dispose() : un Stop() concurrent
        // peut encore appeler Cancel() dessus. Le GC s'en charge.
    }

    private StopReason Loop(RunContext ctx, ISleeper sleeper, CancellationToken ct)
    {
        var observer = Observer;
        double startMs = _clock.NowMs;
        double target = startMs;       // échéance absolue du prochain tick
        double expected = 0;           // intervalle prévu entre le tick précédent et celui-ci
        observer?.OnResync();

        foreach (var tick in ctx.Plan.Enumerate())
        {
            if (!sleeper.SleepUntil(target, ct)) return ctx.RequestedReason;
            if (ctx.MaxRunDuration is { } max && _clock.NowMs - startMs >= max.TotalMilliseconds)
                return StopReason.SafetyTimeout;

            // Filtre de fenêtre : si fermé → pause (touches relâchées), on attend sa réouverture.
            if (!ctx.Gate.CanSend())
            {
                ReleaseAllKeys();
                SetPaused(ctx, true);
                while (!ctx.Gate.CanSend())
                {
                    // Sondage toutes les 5 ms : réactivité correcte sans consommer de CPU.
                    if (ct.WaitHandle.WaitOne(5)) return ctx.RequestedReason;
                    if (ctx.MaxRunDuration is { } m && _clock.NowMs - startMs >= m.TotalMilliseconds)
                        return StopReason.SafetyTimeout;
                }
                SetPaused(ctx, false);
                // Reprise : on repart de « maintenant » (pas de rafale de rattrapage).
                target = _clock.NowMs;
                expected = 0;
                observer?.OnResync();
            }

            var fireMs = _clock.NowMs;
            observer?.OnTick(fireMs, expected);
            Fire(tick, sleeper, ct);
            Interlocked.Increment(ref _ticksSent);

            target += tick.DelayAfterMs;
            expected = tick.DelayAfterMs;

            // Gros retard (PC figé, mise en veille…) : plutôt que d'envoyer une rafale pour rattraper,
            // on recale l'échéance un intervalle après ce tick tardif. Les stats verront une échéance manquée.
            var now = _clock.NowMs;
            if (now - target > tick.DelayAfterMs) target = Math.Max(now, fireMs + tick.DelayAfterMs);
        }
        return StopReason.Completed;
    }

    private void Fire(MacroTick tick, ISleeper sleeper, CancellationToken ct)
    {
        if (tick.IsInstant)
        {
            lock (_keysLock) _sender.Send(tick.InstantActions);
            return;
        }

        foreach (var step in tick.Steps)
        {
            if (step.HoldMs <= 0)
            {
                lock (_keysLock) _sender.Send(new[] { KeyAction.Down(step.Key), KeyAction.Up(step.Key) });
                continue;
            }

            lock (_keysLock)
            {
                _sender.Send(new[] { KeyAction.Down(step.Key) });
                _pressed[step.Key.VirtualKey] = step.Key;
            }
            // Si annulé pendant l'appui : on sort, le finally de Run() relâchera la touche.
            if (!sleeper.SleepUntil(_clock.NowMs + step.HoldMs, ct))
                ct.ThrowIfCancellationRequested();
            lock (_keysLock)
            {
                _sender.Send(new[] { KeyAction.Up(step.Key) });
                _pressed.Remove(step.Key.VirtualKey);
            }
        }
    }

    private void SetPaused(RunContext ctx, bool paused)
    {
        if (_current != ctx) return;
        _state = paused ? EngineState.Paused : EngineState.Running;
        RaiseStateChanged(new EngineStateChangedEventArgs(_state, ctx.Profile, StopReason.None, null));
    }

    private void RaiseStateChanged(EngineStateChangedEventArgs e)
    {
        // Un abonné qui plante ne doit jamais casser le moteur.
        try { StateChanged?.Invoke(this, e); }
        catch { /* volontairement ignoré */ }
    }

    public void Dispose()
    {
        Stop(StopReason.Shutdown);
    }

    private sealed class RunContext
    {
        public RunContext(Profile profile, MacroPlan plan, ISendGate gate, Func<ISleeper> sleeperFactory, TimeSpan? maxRunDuration)
        {
            Profile = profile;
            Plan = plan;
            Gate = gate;
            SleeperFactory = sleeperFactory;
            MaxRunDuration = maxRunDuration;
        }

        public Profile Profile { get; }
        public MacroPlan Plan { get; }
        public ISendGate Gate { get; }
        public Func<ISleeper> SleeperFactory { get; }
        public volatile string? SleeperName;
        public TimeSpan? MaxRunDuration { get; }
        public CancellationTokenSource Cts { get; } = new();
        public Thread? Thread { get; set; }
        public volatile StopReason RequestedReason = StopReason.Manual;
    }
}
