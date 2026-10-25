using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Maquereaux.App.Services;

/// <summary>
/// Effets de fenêtre Windows 11 via DWM : fond Mica, barre de titre sombre, coins arrondis.
/// <para>
/// Mica (matériau translucide teinté par le fond d'écran) n'existe qu'à partir de Windows 11 22H2 (build 22621) :
/// sur les versions antérieures on garde un fond uni, l'appli reste identique à l'œil près.
/// </para>
/// </summary>
public static class WindowEffects
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Mica disponible sur cet OS ?</summary>
    public static bool IsMicaSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Coins arrondis natifs (Windows 11 22000+).</summary>
    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>
    /// Applique Mica (si possible) + mode sombre/clair de la barre système. Retourne true si Mica est actif :
    /// la fenêtre doit alors avoir un fond transparent (le contenu pose un voile teinté par-dessus).
    /// </summary>
    public static bool Apply(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        int darkFlag = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkFlag, sizeof(int));

        if (IsWindows11)
        {
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        }

        if (!IsMicaSupported) return false;
        int backdrop = DWMSBT_MAINWINDOW;
        if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int)) != 0) return false;
        // Le compositeur WPF doit lui aussi peindre en transparent, sinon il masque Mica
        if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target }) target.BackgroundColor = Colors.Transparent;
        return true;
    }
}
