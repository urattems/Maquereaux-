namespace Maquereaux.Core.Models;

/// <summary>Bornes de validation des champs numériques (UI + chargement JSON).</summary>
public static class ProfileLimits
{
    public const int MinIntervalMs = 1;
    public const int MaxIntervalMs = 60_000;
    public const int MinHoldMs = 0;
    public const int MaxHoldMs = 5_000;
    public const int MinRepeatCount = 0;
    public const int MaxRepeatCount = 1_000_000;
    public const int MaxSteps = 64;
    public const int MaxNameLength = 60;

    public static int ClampInterval(int v) => Math.Clamp(v, MinIntervalMs, MaxIntervalMs);
    public static int ClampHold(int v) => Math.Clamp(v, MinHoldMs, MaxHoldMs);
    public static int ClampRepeat(int v) => Math.Clamp(v, MinRepeatCount, MaxRepeatCount);
}
