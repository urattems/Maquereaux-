using Maquereaux.Core.Engine;

namespace Maquereaux.Core.Models;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>Position et taille d'une fenêtre (en pixels indépendants du DPI).</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);

/// <summary>
/// Façon de décrire une touche à Windows. Les deux sont des entrées simulées « honnêtes » (marquées injectées par
/// Windows) ; seule change l'information transmise, pour la compatibilité avec les applis qui ne lisent que l'une.
/// </summary>
public enum KeySendMethod
{
    /// <summary>Scan code (position physique) : le plus compatible avec les jeux (DirectInput / Raw Input).</summary>
    ScanCode,

    /// <summary>Code virtuel + scan code sans le drapeau « scan code » : pour les applis qui lisent le code virtuel.</summary>
    VirtualKey,
}

/// <summary>Réglages globaux de l'application.</summary>
public sealed class AppSettings
{
    /// <summary>Version du format des réglages (migrations au chargement).</summary>
    public const int CurrentVersion = 2;

    /// <summary>0 = fichier antérieur à la v1.6 (ou absent). Voir <see cref="Profiles.ProfileRepository.LoadSettings"/>.</summary>
    public int SettingsVersion { get; set; }

    /// <summary>Arrêt d'urgence, toujours actif (défaut : F9).</summary>
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

    /// <summary>Dernière position/taille de la fenêtre principale (null = centrée, taille par défaut).</summary>
    public WindowPlacement? Window { get; set; }

    // ---------- Interface (v1.1+) ----------

    /// <summary>Barre de navigation latérale repliée (icônes seules).</summary>
    public bool SidebarCollapsed { get; set; }

    public const double MinCompactOpacity = 0.3;

    /// <summary>Opacité de la pastille flottante du mode compact (0,3 à 1).</summary>
    public double CompactOpacity { get; set; } = 0.95;

    /// <summary>L'assistant de premier lancement a été terminé ou passé.</summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>Méthode d'envoi des touches (compatibilité ; scan code par défaut).</summary>
    public KeySendMethod KeySendMethod { get; set; } = KeySendMethod.ScanCode;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
