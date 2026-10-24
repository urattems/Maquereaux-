using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Engine;

/// <summary>Ce qui part à un tick donné : une ou plusieurs étapes, puis le délai jusqu'au tick suivant.</summary>
public sealed class MacroTick
{
    public MacroTick(IReadOnlyList<MacroStep> steps, double delayAfterMs)
    {
        Steps = steps;
        DelayAfterMs = delayAfterMs;
        IsInstant = steps.All(s => s.HoldMs <= 0);
        // Pré-calcul : si aucune étape n'a de durée d'appui, tout le tick = UN seul appel SendInput
        // (down A, up A, down Z, up Z…) → zéro allocation et timing optimal pendant la boucle.
        InstantActions = IsInstant
            ? steps.SelectMany(s => new[] { KeyAction.Down(s.Key), KeyAction.Up(s.Key) }).ToArray()
            : Array.Empty<KeyAction>();
    }

    public IReadOnlyList<MacroStep> Steps { get; }
    public double DelayAfterMs { get; }
    public bool IsInstant { get; }
    public KeyAction[] InstantActions { get; }
}

/// <summary>
/// Plan d'exécution calculé à partir d'un profil (logique pure, testée unitairement) :
/// la liste des ticks d'UN cycle et le nombre total de ticks à jouer.
/// </summary>
public sealed class MacroPlan
{
    private MacroPlan(IReadOnlyList<MacroTick> cycle, long totalTicks)
    {
        Cycle = cycle;
        TotalTicks = totalTicks;
    }

    /// <summary>Ticks d'un cycle complet.</summary>
    public IReadOnlyList<MacroTick> Cycle { get; }

    /// <summary>Nombre total de ticks, ou -1 si infini (RepeatCount = 0).</summary>
    public long TotalTicks { get; }

    public bool IsInfinite => TotalTicks < 0;

    public static MacroPlan Build(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Steps.Count == 0)
            throw new ArgumentException("La séquence est vide : ajoute au moins une touche.", nameof(profile));

        var interval = Math.Max(ProfileLimits.MinIntervalMs, profile.IntervalMs);
        List<MacroTick> cycle;
        if (profile.SequenceMode == SequenceMode.Cycle)
        {
            // Une étape par tick ; chaque étape peut avoir son propre délai.
            cycle = profile.Steps
                .Select(s => new MacroTick(new[] { s }, Math.Max(ProfileLimits.MinIntervalMs, s.DelayAfterMs ?? interval)))
                .ToList();
        }
        else
        {
            // Burst : toute la séquence à chaque tick, cadencée par l'intervalle global.
            cycle = new List<MacroTick> { new(profile.Steps.ToArray(), interval) };
        }

        long total = profile.RepeatCount <= 0 ? -1 : (long)profile.RepeatCount * cycle.Count;
        return new MacroPlan(cycle, total);
    }

    /// <summary>Énumère les ticks dans l'ordre (infini si RepeatCount = 0).</summary>
    public IEnumerable<MacroTick> Enumerate()
    {
        for (long i = 0; IsInfinite || i < TotalTicks; i++)
            yield return Cycle[(int)(i % Cycle.Count)];
    }

    /// <summary>Toutes les touches distinctes utilisées (pour la résolution des scan codes).</summary>
    public IEnumerable<KeyStroke> DistinctKeys =>
        Cycle.SelectMany(t => t.Steps).Select(s => s.Key).DistinctBy(k => k.VirtualKey);
}
