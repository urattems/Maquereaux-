using Maquereaux.Core.Engine;

namespace Maquereaux.Win32;

/// <summary>Crée le sleeper correspondant à la stratégie choisie dans les Paramètres.</summary>
public static class SleeperFactory
{
    public static ISleeper Create(TimerStrategy strategy, IClock clock) => strategy switch
    {
        TimerStrategy.Standard => new StandardSleeper(clock),
        TimerStrategy.HighResolution => new HighResolutionSleeper(clock),
        TimerStrategy.Hybrid => new HybridSleeper(new HighResolutionSleeper(clock), clock, spinMs: 1.0),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    /// <summary>Libellé court pour l'UI.</summary>
    public static string Describe(TimerStrategy strategy) => strategy switch
    {
        TimerStrategy.Standard => "Standard (Thread.Sleep) — imprécis, ~15,6 ms de granularité",
        TimerStrategy.HighResolution => "Haute résolution — précis et économe (recommandé)",
        TimerStrategy.Hybrid => "Hybride — le plus précis, consomme plus de CPU",
        _ => strategy.ToString(),
    };
}
