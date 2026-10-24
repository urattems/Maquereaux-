using Maquereaux.Core.Engine;

namespace Maquereaux.Core.Models;

/// <summary>Réglages globaux de l'application.</summary>
public sealed class AppSettings
{
    /// <summary>Arrêt d'urgence, toujours actif (défaut : Ctrl+Alt+Pause).</summary>
    public Hotkey EmergencyStopHotkey { get; set; } = Hotkey.DefaultEmergencyStop;

    public Guid? ActiveProfileId { get; set; }

    public bool AlwaysOnTop { get; set; }

    /// <summary>Stratégie d'attente du moteur (J2). Haute résolution par défaut : précise et économe.</summary>
    public TimerStrategy TimerStrategy { get; set; } = TimerStrategy.HighResolution;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
