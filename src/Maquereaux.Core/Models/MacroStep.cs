namespace Maquereaux.Core.Models;

/// <summary>Une étape de la séquence : une touche, combien de temps on l'appuie, et le délai qui suit.</summary>
public sealed class MacroStep
{
    public KeyStroke Key { get; set; } = KeyStroke.FromVirtualKey(Input.VirtualKeys.A);

    /// <summary>
    /// Durée entre key-down et key-up (ms). 0 = appui « instantané » : down et up partent dans
    /// le même appel SendInput (le plus précis, parfait pour les applis classiques).
    /// Monter à 5–20 ms si une appli/un jeu « rate » des appuis (lecture d'état une fois par frame).
    /// </summary>
    public int HoldMs { get; set; }

    /// <summary>Délai avant l'étape suivante (mode Cycle). null = intervalle global du profil.</summary>
    public int? DelayAfterMs { get; set; }

    public MacroStep() { }

    public MacroStep(KeyStroke key, int holdMs = 0, int? delayAfterMs = null)
    {
        Key = key;
        HoldMs = holdMs;
        DelayAfterMs = delayAfterMs;
    }

    public MacroStep Clone() => new(Key, HoldMs, DelayAfterMs);
}
