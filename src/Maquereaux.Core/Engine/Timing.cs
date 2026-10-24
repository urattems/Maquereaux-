using System.Diagnostics;

namespace Maquereaux.Core.Engine;

/// <summary>Horloge monotone en millisecondes (abstraite pour pouvoir la simuler dans les tests).</summary>
public interface IClock
{
    /// <summary>Temps écoulé depuis une origine arbitraire, en ms (précision sub-milliseconde).</summary>
    double NowMs { get; }
}

/// <summary>
/// Horloge réelle basée sur <see cref="Stopwatch"/> (QueryPerformanceCounter sous Windows,
/// résolution ~100 ns). C'est LA référence de temps du moteur et des mesures.
/// </summary>
public sealed class StopwatchClock : IClock
{
    public static readonly StopwatchClock Instance = new();
    private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

    public double NowMs => Stopwatch.GetTimestamp() * MsPerTick;

    /// <summary>Convertit un timestamp Stopwatch brut en ms (même base que <see cref="NowMs"/>).</summary>
    public static double TicksToMs(long timestamp) => timestamp * MsPerTick;
}

/// <summary>
/// Stratégie d'attente : « dors jusqu'à l'instant <c>targetMs</c> de l'horloge ».
/// Le moteur vise des échéances ABSOLUES (t0 + n × intervalle) : une attente un peu longue
/// n'est jamais cumulée sur les ticks suivants.
/// </summary>
public interface ISleeper : IDisposable
{
    /// <summary>Nom affiché (UI, benchmarks).</summary>
    string Name { get; }

    /// <summary>Attend jusqu'à <paramref name="targetMs"/>. Retourne false si annulé avant l'échéance.</summary>
    bool SleepUntil(double targetMs, CancellationToken ct);
}

/// <summary>
/// Référence « naïve » : attente via le noyau avec la granularité par défaut de Windows
/// (~15,6 ms). Un Sleep(10) dure donc souvent ~15 ms : c'est précisément le problème que J2 résout.
/// </summary>
public sealed class StandardSleeper : ISleeper
{
    private readonly IClock _clock;

    public StandardSleeper(IClock clock) => _clock = clock;

    public string Name => "Standard (Thread.Sleep)";

    public bool SleepUntil(double targetMs, CancellationToken ct)
    {
        while (true)
        {
            if (ct.IsCancellationRequested) return false;
            var remaining = targetMs - _clock.NowMs;
            if (remaining <= 0) return true;
            // WaitOne(timeout) = Thread.Sleep interruptible par l'annulation.
            // On arrondit au supérieur : un Sleep(0) en boucle brûlerait du CPU pour rien.
            if (ct.WaitHandle.WaitOne((int)Math.Ceiling(remaining))) return false;
        }
    }

    public void Dispose() { }
}
