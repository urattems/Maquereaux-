using System.Windows.Input;

namespace Maquereaux.App.Controls;

internal static class KeyCaptureHelper
{
    /// <summary>
    /// Touche « réelle » d'un KeyEventArgs WPF. Pièges : Alt et F10 arrivent en <c>Key.System</c>
    /// (vraie touche dans SystemKey) ; les IME et touches mortes (^ ¨ en AZERTY) ont leur propre champ.
    /// </summary>
    public static Key RealKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key,
    };

    /// <summary>Code VK Windows d'une touche WPF (0 si inconnu).</summary>
    public static ushort ToVirtualKey(Key key) => (ushort)KeyInterop.VirtualKeyFromKey(key);

    public static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
}
