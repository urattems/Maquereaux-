using System.Text;
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

/// <summary>Raccourci clavier global (ex. F6, Ctrl+Alt+Pause).</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, ushort VirtualKey)
{
    public static Hotkey DefaultToggle => new(HotkeyModifiers.None, VirtualKeys.F6);
    public static Hotkey DefaultEmergencyStop => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKeys.Pause);

    /// <summary>Un raccourci doit avoir une touche principale qui n'est pas un simple modificateur.</summary>
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
