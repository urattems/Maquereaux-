using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Tests.Fakes;

/// <summary>Horloge manuelle : le temps n'avance que quand on le décide.</summary>
public sealed class FakeClock : IClock
{
    private double _now;
    public double NowMs { get => Volatile.Read(ref _now); set => Volatile.Write(ref _now, value); }
    public void Advance(double ms) => NowMs += ms;
}

/// <summary>Sleeper instantané : « dormir jusqu'à T » = avancer l'horloge jusqu'à T (+ un éventuel retard simulé).</summary>
public sealed class FakeSleeper : ISleeper
{
    private readonly FakeClock _clock;
    public FakeSleeper(FakeClock clock) => _clock = clock;

    /// <summary>Retard simulé ajouté à chaque réveil (ms).</summary>
    public Func<double>? Lateness { get; set; }

    /// <summary>Appelé à chaque attente (permet d'injecter un comportement, ex. annuler).</summary>
    public Action<double>? OnSleep { get; set; }

    public int Calls { get; private set; }
    public string Name => "Fake";

    public bool SleepUntil(double targetMs, CancellationToken ct)
    {
        Calls++;
        OnSleep?.Invoke(targetMs);
        if (ct.IsCancellationRequested) return false;
        var late = Lateness?.Invoke() ?? 0;
        if (_clock.NowMs < targetMs + late) _clock.NowMs = targetMs + late;
        return !ct.IsCancellationRequested;
    }

    public void Dispose() { }
}

/// <summary>Enregistre tout ce qui est envoyé, avec l'heure de l'horloge.</summary>
public sealed class RecordingSender : IInputSender
{
    private readonly IClock _clock;
    private readonly object _lock = new();
    public RecordingSender(IClock clock) => _clock = clock;

    public List<(double TimeMs, KeyAction Action)> Sent { get; } = new();
    public List<int> BatchSizes { get; } = new();
    public List<KeyStroke> Prepared { get; } = new();

    /// <summary>Appelé après chaque lot (permet d'arrêter le moteur après N envois, etc.).</summary>
    public Action<RecordingSender>? AfterSend { get; set; }

    /// <summary>Exception à lever au N-ième envoi (simulation de panne).</summary>
    public int? ThrowOnBatch { get; set; }

    public void Prepare(IEnumerable<KeyStroke> keys)
    {
        lock (_lock) Prepared.AddRange(keys);
    }

    public void Send(IReadOnlyList<KeyAction> actions)
    {
        lock (_lock)
        {
            if (ThrowOnBatch == BatchSizes.Count + 1)
            {
                BatchSizes.Add(-1);
                throw new InvalidOperationException("Panne simulée");
            }
            BatchSizes.Add(actions.Count);
            foreach (var a in actions) Sent.Add((_clock.NowMs, a));
        }
        AfterSend?.Invoke(this);
    }

    public List<KeyAction> Actions { get { lock (_lock) return Sent.Select(s => s.Action).ToList(); } }

    /// <summary>Touches « appuyées » (down) dans l'ordre.</summary>
    public List<ushort> DownKeys { get { lock (_lock) return Sent.Where(s => s.Action.IsDown).Select(s => s.Action.Key.VirtualKey).ToList(); } }

    /// <summary>Vrai si chaque key-down a son key-up (pas de touche bloquée).</summary>
    public bool AllKeysReleased
    {
        get
        {
            lock (_lock)
            {
                var pressed = new HashSet<ushort>();
                foreach (var (_, a) in Sent)
                {
                    if (a.IsDown) pressed.Add(a.Key.VirtualKey);
                    else pressed.Remove(a.Key.VirtualKey);
                }
                return pressed.Count == 0;
            }
        }
    }
}

/// <summary>Faux moteur pour tester la logique d'activation.</summary>
public sealed class FakeRunner : IMacroRunner
{
    public bool IsRunning => RunningProfile is not null;
    public Profile? RunningProfile { get; private set; }
    public List<string> Log { get; } = new();
    public ISendGate? LastGate { get; private set; }
    public bool ThrowOnStart { get; set; }

    public void Start(Profile profile, ISendGate? gate = null)
    {
        if (ThrowOnStart) throw new InvalidOperationException("boom");
        RunningProfile = profile;
        LastGate = gate;
        Log.Add($"start:{profile.Name}");
    }

    public void Stop(StopReason reason = StopReason.Manual)
    {
        if (RunningProfile is null) { Log.Add($"stop-noop:{reason}"); return; }
        Log.Add($"stop:{RunningProfile.Name}:{reason}");
        RunningProfile = null;
    }
}

/// <summary>Porte pilotable.</summary>
public sealed class FakeGate : ISendGate
{
    public Func<bool> Predicate { get; set; } = () => true;
    public bool CanSend() => Predicate();
}

public static class TestProfiles
{
    public static readonly KeyStroke A = KeyStroke.FromVirtualKey(VirtualKeys.A);
    public static readonly KeyStroke Z = KeyStroke.FromVirtualKey(VirtualKeys.Z);
    public static readonly KeyStroke E = KeyStroke.FromVirtualKey(VirtualKeys.E);

    public static Profile Aze(SequenceMode mode = SequenceMode.Cycle, int repeat = 0, int interval = 10, int hold = 0) => new()
    {
        Name = "AZE",
        IntervalMs = interval,
        SequenceMode = mode,
        RepeatCount = repeat,
        Steps = { new MacroStep(A, hold), new MacroStep(Z, hold), new MacroStep(E, hold) },
    };
}
