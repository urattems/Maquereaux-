namespace Maquereaux.App.Services;

/// <summary>
/// Signale qu'un champ de capture de touche est actif. Pendant ce temps, les raccourcis globaux des profils
/// sont suspendus (sinon appuyer sur F6 pour le capturer lancerait la macro au lieu d'être capturé).
/// </summary>
public static class KeyCaptureCoordinator
{
    private static int _depth;

    /// <summary>true = une capture commence, false = plus aucune capture.</summary>
    public static event Action<bool>? CaptureChanged;

    public static bool IsCapturing => _depth > 0;

    public static void Begin()
    {
        if (++_depth == 1) CaptureChanged?.Invoke(true);
    }

    public static void End()
    {
        if (_depth == 0) return;
        if (--_depth == 0) CaptureChanged?.Invoke(false);
    }
}
