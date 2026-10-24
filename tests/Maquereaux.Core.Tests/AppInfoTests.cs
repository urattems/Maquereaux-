using Maquereaux.Core;

namespace Maquereaux.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_is_Maquereaux()
    {
        Assert.Equal("Maquereaux", AppInfo.Name);
    }

    [Fact]
    public void Version_has_three_parts()
    {
        Assert.Equal(3, AppInfo.Version.Split('.').Length);
    }
}
