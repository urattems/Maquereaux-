namespace Maquereaux.Win32.Tests;

/// <summary>
/// [Fact] qui ne s'exécute que sous Windows (ignoré proprement sous Linux/macOS).
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Test Win32 : nécessite Windows.";
    }
}
