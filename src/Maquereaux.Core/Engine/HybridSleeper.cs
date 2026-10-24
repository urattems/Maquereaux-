namespace Maquereaux.Core.Engine;

/// <summary>Stratégie d'attente du moteur (choisie dans les Paramètres).</summary>
public enum TimerStrategy
{
    /// <summary>Thread.Sleep : référence, imprécis (~15,6 ms de granularité).</summary>
    Standard,
    /// <summary>Timer noyau haute résolution (Windows 10 1803+) : précis et économe.</summary>
    HighResolution,
    /// <summary>Haute résolution puis attente active la dernière milliseconde : le plus précis, consomme plus de CPU.</summary>
    Hybrid,
}

/// <summary>
/// Attente « hybride » : on dort avec un sleeper grossier jusqu'à ~1 ms de l'échéance, puis on
/// fait une attente active (spin) sur l'horloge pour la fin.
/// <para>
/// Pourquoi : le noyau réveille un thread avec un retard variable (0,1 à 1 ms même en haute résolution).
/// En finissant par une boucle sur le Stopwatch, on tombe pile à quelques microsecondes près.
/// Le prix : un cœur CPU occupé à 100 % pendant la dernière milliseconde de chaque intervalle
/// (≈ 10 % d'un cœur à 10 ms).
/// </para>
/// </summary>
public sealed class HybridSleeper : ISleeper
{
    private readonly ISleeper _coarse;
    private readonly IClock _clock;
    private readonly double _spinMs;

    /// <param name="coarse">Sleeper utilisé pour la partie « dodo » (possédé : disposé avec celui-ci).</param>
    /// <param name="clock">Horloge de référence.</param>
    /// <param name="spinMs">Marge finale en attente active (ms).</param>
    public HybridSleeper(ISleeper coarse, IClock clock, double spinMs = 1.0)
    {
        _coarse = coarse ?? throw new ArgumentNullException(nameof(coarse));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _spinMs = Math.Max(0, spinMs);
    }

    public string Name => $"Hybrid ({_coarse.Name} + spin {_spinMs:0.#} ms)";

    public bool SleepUntil(double targetMs, CancellationToken ct)
    {
        var coarseTarget = targetMs - _spinMs;
        if (coarseTarget > _clock.NowMs && !_coarse.SleepUntil(coarseTarget, ct))
            return false;

        // Attente active : Thread.SpinWait(n) exécute n instructions "pause" sans céder le CPU
        // (contrairement à SpinWait.SpinOnce qui finit par appeler Sleep(1) → imprécis).
        int iterations = 0;
        while (_clock.NowMs < targetMs)
        {
            if ((++iterations & 63) == 0 && ct.IsCancellationRequested) return false;
            Thread.SpinWait(20);
        }
        return !ct.IsCancellationRequested;
    }

    public void Dispose() => _coarse.Dispose();
}
