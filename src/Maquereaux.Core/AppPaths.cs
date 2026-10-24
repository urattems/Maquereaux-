namespace Maquereaux.Core;

/// <summary>Emplacements des fichiers de l'appli (%AppData%\Maquereaux par défaut).</summary>
public sealed class AppPaths
{
    /// <summary>Variable d'environnement qui remplace le dossier de données (tests, smoke test, mode portable).</summary>
    public const string DataDirEnvVar = "MAQUEREAUX_DATA_DIR";

    public AppPaths(string dataDir)
    {
        DataDir = dataDir;
    }

    public string DataDir { get; }
    public string ProfilesFile => Path.Combine(DataDir, "profiles.json");
    public string SettingsFile => Path.Combine(DataDir, "settings.json");
    public string LogsDir => Path.Combine(DataDir, "logs");

    /// <summary>Dossier par défaut, ou celui de <see cref="DataDirEnvVar"/> s'il est défini.</summary>
    public static AppPaths Default()
    {
        var overrideDir = Environment.GetEnvironmentVariable(DataDirEnvVar);
        var dir = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name);
        return new AppPaths(dir);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
    }
}
