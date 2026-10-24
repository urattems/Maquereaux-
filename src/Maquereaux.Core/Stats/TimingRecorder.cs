using Maquereaux.Core.Engine;

namespace Maquereaux.Core.Stats;

/// <summary>
/// Enregistre les intervalles réels entre ticks du moteur (branché sur <see cref="MacroEngine.Observer"/>).
/// Mémoire bornée (tampon circulaire des N derniers intervalles) : peut tourner des heures sans grossir.
/// Thread-safe : écrit par le thread moteur, lu par l'UI (4×/s).
/// </summary>
public sealed class TimingRecorder : ITickObserver
{
    private readonly object _lock = new();
    private readonly (double Actual, double Expected)[] _buffer;
    private int _next;
    private int _count;
    private double _lastTickMs = double.NaN;
    private long _totalTicks;

    public TimingRecorder(int capacity = 10_000)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new (double, double)[capacity];
    }

    public int Capacity => _buffer.Length;

    /// <summary>Nombre total de ticks vus depuis le dernier <see cref="Reset"/>.</summary>
    public long TotalTicks { get { lock (_lock) return _totalTicks; } }

    public void OnResync()
    {
        lock (_lock) _lastTickMs = double.NaN;
    }

    public void OnTick(double nowMs, double expectedIntervalMs)
    {
        lock (_lock)
        {
            _totalTicks++;
            if (!double.IsNaN(_lastTickMs))
            {
                _buffer[_next] = (nowMs - _lastTickMs, expectedIntervalMs);
                _next = (_next + 1) % _buffer.Length;
                if (_count < _buffer.Length) _count++;
            }
            _lastTickMs = nowMs;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _next = 0;
            _count = 0;
            _totalTicks = 0;
            _lastTickMs = double.NaN;
        }
    }

    /// <summary>Copie des intervalles enregistrés, du plus ancien au plus récent.</summary>
    public (double Actual, double Expected)[] GetSamples()
    {
        lock (_lock)
        {
            var result = new (double, double)[_count];
            int start = (_next - _count + _buffer.Length) % _buffer.Length;
            for (int i = 0; i < _count; i++) result[i] = _buffer[(start + i) % _buffer.Length];
            return result;
        }
    }

    public TimingStats Snapshot() => TimingStats.Compute(GetSamples());
}
