using System.Text.Json;
using Maquereaux.Core.Profiles;

namespace Maquereaux.Core.Lab;

/// <summary>Sauvegarde / rechargement explicites d'une session (jamais automatiques : confidentialité).</summary>
public static class LabSessionStore
{
    public const string Extension = ".mqsession.json";

    public static void Save(string path, LabSession session) =>
        JsonFileStore.SaveAtomic(path, new LabSessionDocument { Session = session });

    /// <exception cref="InvalidDataException">Fichier illisible ou invalide.</exception>
    public static LabSession Load(string path)
    {
        try
        {
            var doc = JsonFileStore.Deserialize<LabSessionDocument>(File.ReadAllText(path));
            var s = doc?.Session ?? throw new InvalidDataException("Fichier vide.");
            s.Events ??= new List<CapturedKey>();
            s.Events.RemoveAll(e => e is null);
            s.Name = string.IsNullOrWhiteSpace(s.Name) ? Path.GetFileNameWithoutExtension(path) : s.Name;
            s.Note ??= "";
            if (s.DurationMs <= 0 && s.Events.Count > 0) s.DurationMs = s.Events.Max(e => e.TimeMs);
            return s;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidDataException($"Session illisible : {ex.Message}", ex);
        }
    }
}
