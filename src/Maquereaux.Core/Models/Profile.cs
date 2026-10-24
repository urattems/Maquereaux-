using Maquereaux.Core.Input;

namespace Maquereaux.Core.Models;

/// <summary>Comment la séquence est envoyée à chaque « tick ».</summary>
public enum SequenceMode
{
    /// <summary>Une touche par tick : A → Z → E → A → …</summary>
    Cycle,
    /// <summary>Toute la séquence à chaque tick : (A,Z,E) → (A,Z,E) → …</summary>
    Burst,
}

/// <summary>Comment le raccourci du profil déclenche la macro.</summary>
public enum ActivationMode
{
    /// <summary>Appui = ON, nouvel appui = OFF.</summary>
    Toggle,
    /// <summary>La macro tourne uniquement tant que la touche est maintenue.</summary>
    Hold,
}

/// <summary>Une configuration complète de macro.</summary>
public sealed class Profile
{
    public const int DefaultIntervalMs = 10;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nouveau profil";
    public List<MacroStep> Steps { get; set; } = new();
    public int IntervalMs { get; set; } = DefaultIntervalMs;
    public SequenceMode SequenceMode { get; set; } = SequenceMode.Cycle;

    /// <summary>0 = infini, sinon nombre de cycles complets (Cycle : toute la séquence ; Burst : un tick).</summary>
    public int RepeatCount { get; set; }

    public ActivationMode ActivationMode { get; set; } = ActivationMode.Toggle;
    public Hotkey? Hotkey { get; set; } = Models.Hotkey.DefaultToggle;

    /// <summary>Nom de process (ex. "notepad.exe") : si renseigné, on n'envoie que si cette appli a le focus.</summary>
    public string? TargetProcess { get; set; }

    /// <summary>Copie profonde (le moteur travaille sur un instantané, l'UI peut continuer à éditer).</summary>
    public Profile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Steps = Steps.Select(s => s.Clone()).ToList(),
        IntervalMs = IntervalMs,
        SequenceMode = SequenceMode,
        RepeatCount = RepeatCount,
        ActivationMode = ActivationMode,
        Hotkey = Hotkey,
        TargetProcess = TargetProcess,
    };

    /// <summary>Profil d'exemple : a → z → e toutes les 100 ms (test Bloc-notes du cahier des charges).</summary>
    public static Profile CreateDefault() => new()
    {
        Name = "Profil 1",
        IntervalMs = 100,
        Steps =
        {
            new MacroStep(KeyStroke.FromVirtualKey(VirtualKeys.A)),
            new MacroStep(KeyStroke.FromVirtualKey(VirtualKeys.Z)),
            new MacroStep(KeyStroke.FromVirtualKey(VirtualKeys.E)),
        },
    };
}
