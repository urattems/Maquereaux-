namespace Maquereaux.Core;

/// <summary>Informations générales sur l'application.</summary>
public static class AppInfo
{
    public const string Name = "Maquereaux";

    /// <summary>Version lisible (ex. "1.0.0"), lue depuis l'assembly.</summary>
    public static string Version
    {
        get
        {
            var v = typeof(AppInfo).Assembly.GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
