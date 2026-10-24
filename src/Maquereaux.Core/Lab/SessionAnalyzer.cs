using Maquereaux.Core.Input;
using Maquereaux.Core.Stats;

namespace Maquereaux.Core.Lab;

/// <summary>Stats d'une source (physique, Maquereaux…) dans une session.</summary>
/// <param name="Source">Source concernée (null = toutes sources confondues).</param>
/// <param name="Inputs">Nombre d'appuis « frais » (hors répétitions automatiques).</param>
/// <param name="InputsPerSecond">Cadence moyenne.</param>
/// <param name="Intervals">Intervalles entre deux appuis consécutifs de cette source.</param>
/// <param name="PressDurations">Durées d'appui (down → up).</param>
public sealed record SourceStats(InputSource? Source, int Inputs, double InputsPerSecond, TimingStats Intervals, TimingStats PressDurations);

/// <summary>Point du graphique : à l'instant <c>TimeMs</c>, intervalle depuis l'appui précédent de la même source.</summary>
public readonly record struct IntervalPoint(double TimeMs, double IntervalMs, InputSource Source);

/// <summary>Résultats complets d'une session.</summary>
public sealed class SessionResults
{
    public required LabSession Session { get; init; }
    public required int TotalEvents { get; init; }
    public required int Repeats { get; init; }
    public required SourceStats All { get; init; }
    public required IReadOnlyList<SourceStats> BySource { get; init; }
    public required IReadOnlyList<(string Key, int Count)> ByKey { get; init; }
    public required IReadOnlyList<IntervalPoint> Series { get; init; }

    public int TotalInputs => All.Inputs;
    public double InputsPerSecond => All.InputsPerSecond;
    public double DurationSeconds => Session.DurationMs / 1000.0;

    /// <summary>Source majoritaire (pour nommer la session automatiquement).</summary>
    public InputSource? DominantSource => BySource.OrderByDescending(s => s.Inputs).FirstOrDefault(s => s.Inputs > 0)?.Source;

    public SourceStats? For(InputSource source) => BySource.FirstOrDefault(s => s.Source == source);
}

/// <summary>Calcule les statistiques d'une session (logique pure, testée).</summary>
public static class SessionAnalyzer
{
    public static SessionResults Analyze(LabSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var events = session.Events.OrderBy(e => e.TimeMs).ToList();
        var seconds = session.DurationMs / 1000.0;

        var series = new List<IntervalPoint>();
        var bySource = new List<SourceStats>();
        foreach (var source in Enum.GetValues<InputSource>())
        {
            var evs = events.Where(e => e.Source == source).ToList();
            if (evs.Count == 0) continue;
            var (stats, points) = Compute(evs, source, seconds);
            bySource.Add(stats);
            series.AddRange(points);
        }

        // « Toutes sources » : intervalles entre appuis consécutifs quelle que soit l'origine
        var (all, _) = Compute(events, null, seconds);

        var byKey = events.Where(e => e.IsDown && !e.IsRepeat)
            .GroupBy(e => e.KeyName)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2).ThenBy(x => x.Key)
            .ToList();

        return new SessionResults
        {
            Session = session,
            TotalEvents = events.Count,
            Repeats = events.Count(e => e.IsRepeat),
            All = all,
            BySource = bySource,
            ByKey = byKey,
            Series = series.OrderBy(p => p.TimeMs).ToList(),
        };
    }

    private static (SourceStats Stats, List<IntervalPoint> Points) Compute(List<CapturedKey> evs, InputSource? source, double seconds)
    {
        var downs = evs.Where(e => e.IsDown && !e.IsRepeat).ToList();
        var intervals = new List<double>(Math.Max(0, downs.Count - 1));
        var points = new List<IntervalPoint>();
        for (int i = 1; i < downs.Count; i++)
        {
            var iv = downs[i].TimeMs - downs[i - 1].TimeMs;
            intervals.Add(iv);
            if (source is { } s) points.Add(new IntervalPoint(downs[i].TimeMs, iv, s));
        }

        // Durée d'appui : chaque appui frais est apparié au relâchement suivant de la même touche
        var press = new List<double>();
        var openDown = new Dictionary<ushort, double>();
        foreach (var e in evs)
        {
            if (e.IsDown && !e.IsRepeat) openDown[e.VirtualKey] = e.TimeMs;
            else if (!e.IsDown && openDown.Remove(e.VirtualKey, out var t0)) press.Add(e.TimeMs - t0);
        }

        var rate = seconds > 0 ? downs.Count / seconds : 0;
        return (new SourceStats(source, downs.Count, rate, TimingStats.Compute(intervals), TimingStats.Compute(press)), points);
    }

    /// <summary>Libellé français d'une source.</summary>
    public static string Describe(InputSource? source) => source switch
    {
        InputSource.Physical => "Physique (toi)",
        InputSource.Maquereaux => "Maquereaux",
        InputSource.OtherInjected => "Autre logiciel",
        _ => "Toutes sources",
    };
}
