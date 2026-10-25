using System.Text;
using System.Text.Json.Serialization;
using Maquereaux.Core.Input;

namespace Maquereaux.Core.Models;

/// <summary>Modificateurs d'un raccourci (valeurs identiques aux MOD_* de RegisterHotKey).</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>Raccourci global : une touche (ex. F6, Ctrl+F7) ou un bouton de souris (mode « Maintenir » uniquement).</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, ushort VirtualKey)
{
    public static Hotkey DefaultToggle => new(HotkeyModifiers.None, VirtualKeys.F6);
    public static Hotkey DefaultEmergencyStop => new(HotkeyModifiers.None, VirtualKeys.F9);

    /// <summary>Ancien arrêt d'urgence par défaut (v1.0 → v1.5), migré vers F9 au chargement des réglages.</summary>
    public static Hotkey LegacyEmergencyStop => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKeys.Pause);

    /// <summary>Bouton de souris (RegisterHotKey ne les gère pas : détection par le hook souris, mode Maintenir).</summary>
    [JsonIgnore]
    public bool IsMouse => VirtualKeys.IsMouseButton(VirtualKey);

    /// <summary>Un raccourci doit avoir une touche principale qui n'est pas un simple modificateur.</summary>
    [JsonIgnore]
    public bool IsValid => VirtualKey != 0 && !VirtualKeys.IsModifier(VirtualKey);

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) sb.Append("Win+");
        sb.Append(KeyNames.GetName(VirtualKey));
        return sb.ToString();
    }
}
