using System.Text.Json.Serialization;
using Maquereaux.Core.Input;

namespace Maquereaux.Core.Lab;

/// <summary>Origine d'un événement clavier capturé.</summary>
public enum InputSource
{
    /// <summary>Vraie frappe (toi).</summary>
    Physical,
    /// <summary>Injecté par Maquereaux (SendInput + signature dwExtraInfo).</summary>
    Maquereaux,
    /// <summary>Injecté par un autre logiciel.</summary>
    OtherInjected,
}

/// <summary>Un événement capturé pendant une session du Labo.</summary>
/// <param name="TimeMs">Temps depuis le début de la session (ms, haute résolution).</param>
/// <param name="VirtualKey">Code VK.</param>
/// <param name="ScanCode">Scan code.</param>
/// <param name="IsDown">Appui (true) ou relâchement (false).</param>
/// <param name="Source">Physique / Maquereaux / autre injecté.</param>
/// <param name="IsRepeat">Répétition automatique du clavier (touche maintenue) : pas un nouvel « input ».</param>
public sealed record CapturedKey(double TimeMs, ushort VirtualKey, ushort ScanCode, bool IsDown, InputSource Source, bool IsRepeat)
{
    [JsonIgnore]
    public string KeyName => KeyNames.GetName(VirtualKey);
}

/// <summary>Une session d'enregistrement (gardée en mémoire ; écrite sur disque uniquement à la demande).</summary>
public sealed class LabSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Session";
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>Durée effective de la session (ms).</summary>
    public double DurationMs { get; set; }

    /// <summary>Note libre (ex. « mannequin, macro officielle, 20 s → 12 340 dégâts »).</summary>
    public string Note { get; set; } = "";

    public List<CapturedKey> Events { get; set; } = new();
}

/// <summary>Fichier de session sauvegardé (.mqsession.json).</summary>
public sealed class LabSessionDocument
{
    public int Version { get; set; } = 1;
    public string App { get; set; } = AppInfo.Name;
    public LabSession? Session { get; set; }
}
