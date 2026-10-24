using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maquereaux.Core.Profiles;

public enum LoadStatus
{
    /// <summary>Fichier lu correctement.</summary>
    Loaded,
    /// <summary>Pas de fichier : valeur par défaut.</summary>
    Missing,
    /// <summary>Fichier illisible : sauvegardé à part, valeur par défaut utilisée.</summary>
    Corrupted,
}

public sealed record LoadResult<T>(T Value, LoadStatus Status, string? BackupPath = null, string? Error = null);

/// <summary>
/// Lecture/écriture JSON robustes.
/// <list type="bullet">
/// <item>Écriture ATOMIQUE : on écrit dans un fichier temporaire puis on le renomme par-dessus l'ancien.
/// Si le PC plante en pleine écriture, on garde l'ancien fichier intact (jamais un fichier à moitié écrit).</item>
/// <item>Lecture tolérante : JSON corrompu → copie de secours « .corrupt-…bak » + valeur par défaut, sans crash.</item>
/// </list>
/// </summary>
public static class JsonFileStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static LoadResult<T> Load<T>(string path, Func<T> defaultFactory) where T : class
    {
        if (!File.Exists(path)) return new(defaultFactory(), LoadStatus.Missing);
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var value = JsonSerializer.Deserialize<T>(json, Options);
            if (value is null) throw new JsonException("Contenu vide (null).");
            return new(value, LoadStatus.Loaded);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            var backup = Backup(path);
            return new(defaultFactory(), LoadStatus.Corrupted, backup, ex.Message);
        }
    }

    public static void SaveAtomic<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(fs, value, Options);
                fs.Flush(flushToDisk: true);
            }
            // Renommage par-dessus l'existant (MoveFileEx REPLACE_EXISTING sous Windows) : opération atomique
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    private static string? Backup(string path)
    {
        try
        {
            var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss-fff}.bak";
            File.Copy(path, backup, overwrite: true);
            return backup;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
