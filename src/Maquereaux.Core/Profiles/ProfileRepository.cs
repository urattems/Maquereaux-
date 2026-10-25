using System.Text.Json;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Profiles;

/// <summary>Contenu de profiles.json (et des fichiers d'export).</summary>
public sealed class ProfileDocument
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string App { get; set; } = AppInfo.Name;
    public List<Profile> Profiles { get; set; } = new();
}

/// <summary>Chargement/sauvegarde des profils et réglages dans %AppData%\Maquereaux, import/export.</summary>
public sealed class ProfileRepository
{
    private readonly AppPaths _paths;

    public ProfileRepository(AppPaths paths) => _paths = paths;

    /// <summary>Messages à afficher après le chargement (fichier corrompu restauré, etc.).</summary>
    public List<string> Warnings { get; } = new();

    public List<Profile> LoadProfiles()
    {
        var r = JsonFileStore.Load(_paths.ProfilesFile, () => new ProfileDocument());
        if (r.Status == LoadStatus.Corrupted)
            Warnings.Add($"profiles.json était illisible ({r.Error}). Une copie a été gardée ({Path.GetFileName(r.BackupPath)}) et un profil par défaut a été créé.");
        return ProfileValidator.SanitizeAll(r.Value.Profiles);
    }

    public void SaveProfiles(IEnumerable<Profile> profiles) =>
        JsonFileStore.SaveAtomic(_paths.ProfilesFile, new ProfileDocument { Profiles = profiles.Select(p => p.Clone()).ToList() });

    public AppSettings LoadSettings()
    {
        var r = JsonFileStore.Load(_paths.SettingsFile, () => new AppSettings());
        if (r.Status == LoadStatus.Corrupted)
            Warnings.Add($"settings.json était illisible ({r.Error}). Réglages par défaut restaurés (copie : {Path.GetFileName(r.BackupPath)}).");
        var s = r.Value;
        // L'arrêt d'urgence passe par RegisterHotKey : une touche du clavier obligatoirement (pas un bouton de souris)
        if (s.EmergencyStopHotkey is null || !s.EmergencyStopHotkey.IsValid || s.EmergencyStopHotkey.IsMouse)
            s.EmergencyStopHotkey = Hotkey.DefaultEmergencyStop;
        if (s.SettingsVersion < 2)
        {
            // v1.6 : l'arrêt d'urgence par défaut passe de Ctrl+Alt+Pause à F9 (une seule touche, accessible en jeu).
            // Seul l'ancien défaut est migré : un raccourci choisi à la main est conservé.
            if (s.EmergencyStopHotkey == Hotkey.LegacyEmergencyStop) s.EmergencyStopHotkey = Hotkey.DefaultEmergencyStop;
            s.SettingsVersion = AppSettings.CurrentVersion;
        }
        if (!Enum.IsDefined(s.TimerStrategy)) s.TimerStrategy = Engine.TimerStrategy.HighResolution;
        if (!Enum.IsDefined(s.Theme)) s.Theme = AppTheme.Dark;
        s.CompactOpacity = double.IsFinite(s.CompactOpacity) ? Math.Clamp(s.CompactOpacity, AppSettings.MinCompactOpacity, 1) : 0.95;
        s.SafetyTimeoutMinutes = Math.Clamp(s.SafetyTimeoutMinutes, AppSettings.MinSafetyTimeoutMinutes, AppSettings.MaxSafetyTimeoutMinutes);
        return s;
    }

    public void SaveSettings(AppSettings settings) => JsonFileStore.SaveAtomic(_paths.SettingsFile, settings);

    /// <summary>Exporte des profils dans un fichier JSON (format identique à profiles.json).</summary>
    public static void Export(string path, IEnumerable<Profile> profiles) =>
        JsonFileStore.SaveAtomic(path, new ProfileDocument { Profiles = profiles.Select(p => p.Clone()).ToList() });

    /// <summary>
    /// Importe des profils. Accepte un document complet ou un profil seul.
    /// Les Ids déjà présents dans <paramref name="existingIds"/> sont régénérés (l'import n'écrase jamais).
    /// </summary>
    /// <exception cref="InvalidDataException">Fichier illisible ou sans profil.</exception>
    public static List<Profile> Import(string path, IEnumerable<Guid> existingIds)
    {
        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Lecture impossible : {ex.Message}", ex);
        }

        List<Profile?> raw;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            raw = doc.RootElement.ValueKind == JsonValueKind.Object && HasProperty(doc.RootElement, "profiles")
                ? JsonFileStore.Deserialize<ProfileDocument>(json)?.Profiles.Cast<Profile?>().ToList() ?? new()
                : new List<Profile?> { JsonFileStore.Deserialize<Profile>(json) };
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new InvalidDataException($"JSON invalide : {ex.Message}", ex);
        }

        var valid = raw.Where(p => p is not null).Select(p => p!).ToList();
        if (valid.Count == 0) throw new InvalidDataException("Aucun profil trouvé dans ce fichier.");

        var taken = new HashSet<Guid>(existingIds);
        foreach (var p in valid)
        {
            ProfileValidator.Sanitize(p);
            if (!taken.Add(p.Id))
            {
                p.Id = Guid.NewGuid();
                taken.Add(p.Id);
            }
        }
        return valid;
    }

    private static bool HasProperty(JsonElement e, string name) =>
        e.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
