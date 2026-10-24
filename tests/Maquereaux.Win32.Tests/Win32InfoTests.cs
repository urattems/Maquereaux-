namespace Maquereaux.Win32.Tests;

public class Win32InfoTests
{
    [WindowsFact]
    public void Modern_windows_supports_high_resolution_timer()
    {
        Assert.True(Win32Info.SupportsHighResolutionWaitableTimer);
    }
}
