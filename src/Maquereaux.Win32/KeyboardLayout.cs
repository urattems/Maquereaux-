using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

/// <summary>Conversions VK ↔ scan code selon la disposition clavier (AZERTY, QWERTY…).</summary>
public static class KeyboardLayout
{
    /// <summary>
    /// Disposition clavier de la fenêtre au premier plan. La disposition est « par thread » sous Windows :
    /// c'est celle de l'appli cible qui compte pour traduire un scan code en caractère.
    /// </summary>
    public static IntPtr GetForegroundLayout()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        var tid = hwnd == IntPtr.Zero ? 0 : NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        return NativeMethods.GetKeyboardLayout(tid);
    }

    /// <summary>
    /// Scan code d'un VK dans une disposition donnée. MAPVK_VK_TO_VSC_EX renvoie 0xE0xx pour
    /// les touches étendues (flèches, Inser…) → on en déduit le flag « extended ».
    /// Retourne Scan = 0 si pas de scan code exploitable (ex. Pause) : on enverra alors en mode VK.
    /// </summary>
    public static (ushort Scan, bool Extended) Resolve(ushort vk, IntPtr hkl)
    {
        // Pause a une séquence à part (E1 1D 45) que KEYEVENTF_SCANCODE ne sait pas rejouer.
        if (vk is VirtualKeys.Pause or VirtualKeys.Cancel) return (0, KeyNames.IsExtendedKey(vk));
        var raw = NativeMethods.MapVirtualKeyExW(vk, NativeMethods.MAPVK_VK_TO_VSC_EX, hkl);
        if (raw == 0) return (0, KeyNames.IsExtendedKey(vk));
        var prefix = (raw >> 8) & 0xFF;
        var extended = prefix is 0xE0 or 0xE1 || KeyNames.IsExtendedKey(vk);
        return ((ushort)(raw & 0xFF), extended);
    }

    /// <summary>Nom localisé d'une touche via GetKeyNameText (ex. "²", "^", "Ù" en AZERTY).</summary>
    public static unsafe string? GetKeyName(ushort scan, bool extended)
    {
        if (scan == 0) return null;
        int lParam = (scan << 16) | (extended ? 1 << 24 : 0);
        char* buffer = stackalloc char[64];
        int len = NativeMethods.GetKeyNameTextW(lParam, buffer, 64);
        return len > 0 ? new string(buffer, 0, len) : null;
    }

    /// <summary>
    /// Décrit complètement une touche capturée : VK + scan code + étendue + nom affichable.
    /// Pour les touches « caractère » (lettres, ponctuation OEM) on prend le nom localisé Windows,
    /// pour les touches spéciales (F1, flèches…) nos noms français.
    /// </summary>
    public static KeyStroke Describe(ushort vk)
    {
        var (scan, ext) = Resolve(vk, GetForegroundLayout());
        var defaultName = KeyNames.GetName(vk);
        bool isCharacterKey = (vk >= 0x30 && vk <= 0x5A) || (vk >= 0xBA && vk <= 0xE2);
        string name = defaultName;
        if (isCharacterKey || defaultName.StartsWith("VK ", StringComparison.Ordinal))
        {
            var localized = GetKeyName(scan, ext);
            if (!string.IsNullOrWhiteSpace(localized))
                name = localized.Length == 1 ? localized.ToUpperInvariant() : localized;
        }
        return new KeyStroke(vk, scan, ext, name);
    }
}
