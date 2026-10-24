using Maquereaux.Core.Engine;

namespace Maquereaux.Core.Models;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>Réglages globaux de l'application.</summary>
public sealed class AppSettings
{
    /// <summary>Arrêt d'urgence, toujours actif (défaut : Ctrl+Alt+Pause).</summary>
    public Hotkey EmergencyStopHotkey { get; set; } = Hotkey.DefaultEmergencyStop;

    public Guid? ActiveProfileId { get; set; }

    public bool AlwaysOnTop { get; set; }

    /// <summary>Stratégie d'attente du moteur (J2). Haute résolution par défaut : précise et économe.</summary>
    public TimerStrategy TimerStrategy { get; set; } = TimerStrategy.HighResolution;

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public const int MinSafetyTimeoutMinutes = 1;
    public const int MaxSafetyTimeoutMinutes = 240;

    /// <summary>
    /// Garde-fou : une exécution s'arrête toute seule au bout de N minutes. Maquereaux n'est pas fait pour
    /// tourner sans surveillance (voir « Hors périmètre » : pas de farm AFK).
    /// </summary>
    public int SafetyTimeoutMinutes { get; set; } = 60;

    /// <summary>Réduire dans la zone de notification au lieu de la barre des tâches.</summary>
    public bool MinimizeToTray { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
