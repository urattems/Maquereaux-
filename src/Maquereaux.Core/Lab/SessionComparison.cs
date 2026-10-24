using System.Globalization;

namespace Maquereaux.Core.Lab;

/// <summary>Une ligne du tableau comparatif A vs B.</summary>
/// <param name="Metric">Nom de la métrique.</param>
/// <param name="A">Valeur session A.</param>
/// <param name="B">Valeur session B.</param>
/// <param name="Unit">Unité affichée.</param>
/// <param name="LowerIsBetter">Pour colorer le delta : un jitter plus bas est « mieux », une cadence plus haute aussi.</param>
public sealed record ComparisonRow(string Metric, double A, double B, string Unit, bool LowerIsBetter)
{
    public double Delta => B - A;

    /// <summary>Écart relatif (B vs A) en %, NaN si A = 0.</summary>
    public double DeltaPercent => A == 0 ? double.NaN : (B - A) / Math.Abs(A) * 100;

    /// <summary>true si B est meilleur que A, false si pire, null si égal.</summary>
    public bool? BIsBetter => Math.Abs(Delta) < 1e-9 ? null : LowerIsBetter ? B < A : B > A;

    public string Format(double v) => double.IsNaN(v) ? "—" : v.ToString(Unit == "" ? "0" : "0.###", CultureInfo.CurrentCulture) + (Unit == "" ? "" : " " + Unit);
    public string AText => Format(A);
    public string BText => Format(B);
    public string DeltaText =>
        (Delta > 0 ? "+" : "") + Format(Delta) + (double.IsNaN(DeltaPercent) ? "" : $" ({DeltaPercent:+0.#;-0.#;0} %)");
}

/// <summary>Comparaison de deux sessions côte à côte (ex. « Manuel 20 s » vs « Maquereaux 20 s »).</summary>
public static class SessionComparison
{
    public static IReadOnlyList<ComparisonRow> Compare(SessionResults a, SessionResults b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new List<ComparisonRow>
        {
            new("Durée", a.DurationSeconds, b.DurationSeconds, "s", false),
            new("Inputs", a.TotalInputs, b.TotalInputs, "", false),
            new("Inputs / seconde", a.InputsPerSecond, b.InputsPerSecond, "/s", false),
            new("Intervalle moyen", a.All.Intervals.Mean, b.All.Intervals.Mean, "ms", true),
            new("Intervalle min", a.All.Intervals.Min, b.All.Intervals.Min, "ms", true),
            new("Intervalle max", a.All.Intervals.Max, b.All.Intervals.Max, "ms", true),
            new("p95", a.All.Intervals.P95, b.All.Intervals.P95, "ms", true),
            new("Jitter (σ)", a.All.Intervals.StdDev, b.All.Intervals.StdDev, "ms", true),
            new("Durée d'appui moyenne", a.All.PressDurations.Mean, b.All.PressDurations.Mean, "ms", true),
        };
    }
}
