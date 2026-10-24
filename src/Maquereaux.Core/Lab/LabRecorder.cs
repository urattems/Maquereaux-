using Maquereaux.Core.Input;

namespace Maquereaux.Core.Lab;

public enum LabRecorderState
{
    Idle,
    Recording,
}

/// <summary>
/// Enregistreur de session du Labo. Reçoit les événements du hook (thread du hook) et les commandes
/// Start/Stop/Tick (thread UI) → protégé par un verrou.
/// <para>
/// Confidentialité : n'enregistre RIEN hors d'une session lancée manuellement (état <see cref="LabRecorderState.Recording"/>).
/// </para>
/// </summary>
public sealed class LabRecorder
{
    private readonly object _lock = new();
    private readonly HashSet<ushort> _pressed = new();
    private LabSession? _session;
    private double _startMs;
    private double? _durationMs;

    public LabRecorderState State { get; private set; } = LabRecorderState.Idle;

    /// <summary>Session terminée (durée fixe atteinte ou Stop). Levé sur le thread qui a provoqué la fin.</summary>
    public event Action<LabSession>? Finished;

    /// <summary>Nombre d'événements capturés dans la session en cours.</summary>
    public int EventCount { get { lock (_lock) return _session?.Events.Count ?? 0; } }

    /// <summary>Temps écoulé depuis le début de la session (ms).</summary>
    public double Elapsed(double nowMs)
    {
        lock (_lock)
        {
            if (State != LabRecorderState.Recording) return 0;
            var e = nowMs - _startMs;
            return _durationMs is { } d ? Math.Min(e, d) : e;
        }
    }

    /// <summary>Démarre une session à <paramref name="nowMs"/> ; durée fixe optionnelle.</summary>
    public void Start(double nowMs, string name, double? durationMs = null)
    {
        if (durationMs is <= 0) throw new ArgumentOutOfRangeException(nameof(durationMs));
        lock (_lock)
        {
            if (State == LabRecorderState.Recording) throw new InvalidOperationException("Une session est déjà en cours.");
            _session = new LabSession { Name = name, StartedAt = DateTime.Now };
            _startMs = nowMs;
            _durationMs = durationMs;
            _pressed.Clear();
            State = LabRecorderState.Recording;
        }
    }

    /// <summary>Événement du hook. Ignoré hors session ou au-delà de la durée fixe.</summary>
    public void OnKeyEvent(in RawKeyEvent e)
    {
        LabSession? finished = null;
        lock (_lock)
        {
            if (State != LabRecorderState.Recording || _session is null) return;
            var t = e.TimestampMs - _startMs;
            if (t < 0) return; // événement antérieur au démarrage (encore dans la file du hook)
            if (_durationMs is { } d && t > d)
            {
                finished = FinishLocked(_startMs + d);
            }
            else
            {
                bool repeat = false;
                if (e.IsDown) repeat = !_pressed.Add(e.VirtualKey);
                else _pressed.Remove(e.VirtualKey);
                var source = !e.IsInjected ? InputSource.Physical
                    : e.IsFromMaquereaux ? InputSource.Maquereaux
                    : InputSource.OtherInjected;
                _session.Events.Add(new CapturedKey(t, e.VirtualKey, e.ScanCode, e.IsDown, source, repeat));
            }
        }
        if (finished is not null) Finished?.Invoke(finished);
    }

    /// <summary>À appeler régulièrement (ex. 10×/s) : termine la session quand la durée fixe est écoulée.</summary>
    public void Tick(double nowMs)
    {
        LabSession? finished = null;
        lock (_lock)
        {
            if (State == LabRecorderState.Recording && _durationMs is { } d && nowMs - _startMs >= d)
                finished = FinishLocked(_startMs + d);
        }
        if (finished is not null) Finished?.Invoke(finished);
    }

    /// <summary>Arrêt manuel. Retourne la session (ou null si rien en cours).</summary>
    public LabSession? Stop(double nowMs)
    {
        LabSession? finished;
        lock (_lock)
        {
            if (State != LabRecorderState.Recording) return null;
            var end = _durationMs is { } d ? Math.Min(nowMs, _startMs + d) : nowMs;
            finished = FinishLocked(end);
        }
        Finished?.Invoke(finished);
        return finished;
    }

    private LabSession FinishLocked(double endMs)
    {
        var s = _session!;
        s.DurationMs = Math.Max(0, endMs - _startMs);
        _session = null;
        _pressed.Clear();
        State = LabRecorderState.Idle;
        return s;
    }
}
