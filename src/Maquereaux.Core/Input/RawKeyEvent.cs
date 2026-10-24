namespace Maquereaux.Core.Input;

/// <summary>
/// Événement clavier brut capturé par le hook bas niveau (WH_KEYBOARD_LL).
/// </summary>
/// <param name="TimestampMs">Horodatage haute résolution (même base que <see cref="Engine.StopwatchClock"/>).</param>
/// <param name="VirtualKey">Code VK.</param>
/// <param name="ScanCode">Scan code matériel.</param>
/// <param name="IsDown">true = appui, false = relâchement.</param>
/// <param name="IsInjected">Événement synthétique (SendInput…) et non une vraie frappe (flag LLKHF_INJECTED).</param>
/// <param name="IsFromMaquereaux">Injecté par Maquereaux (signature dans dwExtraInfo).</param>
/// <param name="IsExtended">Touche étendue (flèches, Inser…).</param>
public readonly record struct RawKeyEvent(
    double TimestampMs,
    ushort VirtualKey,
    ushort ScanCode,
    bool IsDown,
    bool IsInjected,
    bool IsFromMaquereaux = false,
    bool IsExtended = false);
