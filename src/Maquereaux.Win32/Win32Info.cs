namespace Maquereaux.Win32;

/// <summary>Infos sur l'environnement Windows courant.</summary>
public static class Win32Info
{
    /// <summary>
    /// Vrai si l'OS supporte CREATE_WAITABLE_TIMER_HIGH_RESOLUTION (Windows 10 1803 = build 17134).
    /// </summary>
    public static bool SupportsHighResolutionWaitableTimer =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134);
}
