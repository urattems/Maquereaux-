namespace Maquereaux.Core.Stats;

/// <summary>
/// Statistiques d'une série d'intervalles (ms) : moyenne, min, max, percentiles, jitter.
/// </summary>
/// <param name="Count">Nombre d'intervalles mesurés.</param>
/// <param name="Mean">Moyenne.</param>
/// <param name="Min">Minimum.</param>
/// <param name="Max">Maximum.</param>
/// <param name="P50">Médiane.</param>
/// <param name="P95">95e percentile : 95 % des intervalles sont inférieurs ou égaux.</param>
/// <param name="P99">99e percentile.</param>
/// <param name="StdDev">Écart-type (= « jitter » : à quel point les intervalles varient).</param>
/// <param name="Missed">Échéances manquées : intervalles &gt; prévu × 1,5.</param>
public sealed record TimingStats(int Count, double Mean, double Min, double Max, double P50, double P95, double P99, double StdDev, int Missed)
{
    /// <summary>Seuil d'échéance manquée : intervalle réel &gt; intervalle prévu × 1,5.</summary>
    public const double MissFactor = 1.5;

    public static readonly TimingStats Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Fréquence moyenne correspondante (Hz), 0 si inconnue.</summary>
    public double RateHz => Mean > 0 ? 1000.0 / Mean : 0;

    public bool IsEmpty => Count == 0;

    /// <summary>Calcule les stats d'une série d'intervalles ; <paramref name="expectedMs"/> sert au comptage des échéances manquées.</summary>
    public static TimingStats Compute(IEnumerable<double> intervals, double? expectedMs = null) =>
        Compute(intervals.Select(i => (i, expectedMs ?? 0)));

    /// <summary>Variante où chaque intervalle a son propre intervalle prévu (0 = inconnu, non compté dans Missed).</summary>
    public static TimingStats Compute(IEnumerable<(double Actual, double Expected)> samples)
    {
        var list = samples as IList<(double Actual, double Expected)> ?? samples.ToList();
        int n = list.Count;
        if (n == 0) return Empty;

        var sorted = new double[n];
        double sum = 0;
        int missed = 0;
        for (int i = 0; i < n; i++)
        {
            var (a, e) = list[i];
            sorted[i] = a;
            sum += a;
            if (e > 0 && a > e * MissFactor) missed++;
        }
        Array.Sort(sorted);
        double mean = sum / n;

        // Écart-type « population » : on décrit la série mesurée elle-même, pas un échantillon.
        double sq = 0;
        foreach (var v in sorted) sq += (v - mean) * (v - mean);
        double std = Math.Sqrt(sq / n);

        return new TimingStats(n, mean, sorted[0], sorted[^1],
            Percentile(sorted, 50), Percentile(sorted, 95), Percentile(sorted, 99), std, missed);
    }

    /// <summary>
    /// Percentile par interpolation linéaire entre rangs (même méthode que PERCENTILE.INC d'Excel).
    /// <paramref name="sorted"/> doit être trié croissant.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> sorted, double percent)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];
        double rank = Math.Clamp(percent, 0, 100) / 100.0 * (sorted.Count - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        double frac = rank - lo;
        return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
    }
}
