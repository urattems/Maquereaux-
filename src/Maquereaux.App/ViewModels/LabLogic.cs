using System.Globalization;
using Maquereaux.Core.Lab;

namespace Maquereaux.App.ViewModels;

/// <summary>Logique pure de l'écran Labo (verdict de comparaison), testée unitairement.</summary>
public static class LabLogic
{
    /// <summary>En dessous de ±15 % d'écart, on considère les deux sessions équivalentes.</summary>
    private const double Threshold = 1.15;

    /// <summary>
    /// Verdict en une phrase : « Maquereaux est 23× plus régulier que Manuel (σ 0,25 ms contre 5,8 ms) et 1,8× plus rapide. »
    /// Chaîne vide si la comparaison est incomplète.
    /// </summary>
    public static string Verdict(IReadOnlyList<ComparisonRow> rows, string nameA, string nameB)
    {
        var rate = rows.FirstOrDefault(r => r.Metric == "Inputs / seconde");
        var jitter = rows.FirstOrDefault(r => r.Metric == "Jitter (σ)");
        var inputs = rows.FirstOrDefault(r => r.Metric == "Inputs");
        if (rate is null || jitter is null || inputs is null) return "";
        if (inputs.A < 2 || inputs.B < 2) return "Une des deux sessions a trop peu d'appuis pour être comparée.";

        var c = CultureInfo.CurrentCulture;
        string Ms(double v) => v.ToString(v < 10 ? "0.##" : "0.#", c) + " ms";
        string Times(double r) => r.ToString(r >= 10 ? "0" : "0.#", c) + "×";

        string regularity;
        if (jitter.B <= 1e-9 && jitter.A <= 1e-9) regularity = "aussi régulier que";
        else if (jitter.B <= 1e-9) regularity = "parfaitement régulier, contrairement à";
        else
        {
            double r = jitter.A / jitter.B;
            regularity = r >= Threshold ? $"{Times(r)} plus régulier que"
                : r <= 1 / Threshold ? $"{Times(1 / r)} moins régulier que"
                : "aussi régulier que";
        }

        string speed;
        if (rate.A <= 1e-9) speed = "";
        else
        {
            double r = rate.B / rate.A;
            speed = r >= Threshold ? $" et {Times(r)} plus rapide"
                : r <= 1 / Threshold ? $" et {Times(1 / r)} plus lent"
                : ", à cadence égale";
        }

        return $"{nameB} est {regularity} {nameA} (σ {Ms(jitter.B)} contre {Ms(jitter.A)}){speed}.";
    }
}
