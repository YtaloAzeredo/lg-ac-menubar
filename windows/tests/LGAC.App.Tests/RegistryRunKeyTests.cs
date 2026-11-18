using LGAC.App.Platform;
using Microsoft.Win32;

namespace LGAC.App.Tests;

[TestClass]
public sealed class RegistryRunKeyTests
{
    private readonly string subKeyPath = @"Software\LGAC.Tests." + Guid.NewGuid();

    [TestCleanup]
    public void Cleanup()
    {
        Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
    }

    [TestMethod]
    public void GetIsNullWhenMissing()
    {
        var key = new RegistryRunKey(subKeyPath);

        Assert.IsNull(key.Get("LG AC"));
    }

    [TestMethod]
    public void SetThenGetRoundTrips()
    {
        var key = new RegistryRunKey(subKeyPath);

        key.Set("LG AC", "\"C:\\Apps\\LG AC.exe\"");

        Assert.AreEqual("\"C:\\Apps\\LG AC.exe\"", key.Get("LG AC"));
    }

    [TestMethod]
    public void RemoveDeletesValueAndIsIdempotent()
    {
        var key = new RegistryRunKey(subKeyPath);
        key.Set("LG AC", "value");

        key.Remove("LG AC");
        key.Remove("LG AC");

        Assert.IsNull(key.Get("LG AC"));
    }
}
