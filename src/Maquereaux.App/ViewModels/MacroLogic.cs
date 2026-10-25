using System.Globalization;
using Maquereaux.Core.Models;

namespace Maquereaux.App.ViewModels;

/// <summary>Logique pure de l'écran Macro (presets, équivalences, conseils) : sans WPF, testée unitairement.</summary>
public static class MacroLogic
{
    /// <summary>Intervalles proposés en un clic (ms) : 100/s, ~60 fps, ~30 fps, 20/s, 10/s.</summary>
    public static readonly IReadOnlyList<int> IntervalPresets = new[] { 10, 16, 33, 50, 100 };

    /// <summary>Durée d'appui recommandée pour les jeux qui lisent le clavier une fois par image.</summary>
    public const int RecommendedGameHoldMs = 16;

    /// <summary>Nombre d'appuis par seconde produits par le profil (Burst : toute la séquence à chaque tick).</summary>
    public static double InputsPerSecond(int intervalMs, SequenceMode mode, int steps)
    {
        if (intervalMs <= 0) return 0;
        var ticksPerSecond = 1000.0 / intervalMs;
        return mode == SequenceMode.Burst ? ticksPerSecond * Math.Max(1, steps) : ticksPerSecond;
    }

    /// <summary>Phrase d'équivalence affichée sous l'intervalle (« = 100 inputs/s »).</summary>
    public static string EquivalenceText(int intervalMs, SequenceMode mode, int steps)
    {
        var rate = InputsPerSecond(intervalMs, mode, steps);
        var formatted = rate >= 10 || Math.Abs(rate - Math.Round(rate)) < 0.05
            ? Math.Round(rate).ToString("N0", CultureInfo.CurrentCulture)
            : rate.ToString("0.#", CultureInfo.CurrentCulture);
        return $"= {formatted} input{(rate >= 2 ? "s" : "")}/s";
    }

    /// <summary>
    /// Applis « de bureau » qui lisent les messages clavier (un appui de 0 ms leur suffit). Toute autre cible
    /// est supposée pouvoir être un jeu qui sonde l'état du clavier une fois par image.
    /// </summary>
    private static readonly HashSet<string> DesktopApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "notepad.exe", "notepad++.exe", "wordpad.exe", "winword.exe", "excel.exe", "powerpnt.exe", "outlook.exe",
        "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "explorer.exe", "code.exe", "devenv.exe",
        "discord.exe", "slack.exe", "teams.exe", "ms-teams.exe", "windowsterminal.exe", "cmd.exe", "powershell.exe", "pwsh.exe",
    };

    /// <summary>
    /// Faut-il afficher le conseil « certains jeux ratent les appuis instantanés » ? Oui si au moins une étape a un
    /// appui de 0 ms ET que la fenêtre cible n'est pas une appli de bureau connue.
    /// </summary>
    public static bool ShouldAdviseHold(Profile profile)
    {
        if (profile.Steps.Count == 0 || !profile.Steps.Any(s => s.HoldMs == 0)) return false;
        return !string.IsNullOrEmpty(profile.TargetProcess) && !DesktopApps.Contains(profile.TargetProcess);
    }

    /// <summary>Libellé de puce pour la durée d'appui (« 0 ms » = instantané).</summary>
    public static string HoldChip(int holdMs) => holdMs == 0 ? "instantané" : $"{holdMs} ms";

    /// <summary>Libellé de puce pour le délai après l'étape (null = intervalle global).</summary>
    public static string DelayChip(int? delayMs, int intervalMs) => delayMs is { } d ? $"→ {d} ms" : $"→ {intervalMs} ms";
}
