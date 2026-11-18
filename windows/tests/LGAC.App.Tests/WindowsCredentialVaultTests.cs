using LGAC.App.Platform;

namespace LGAC.App.Tests;

[TestClass]
public sealed class WindowsCredentialVaultTests
{
    private readonly WindowsCredentialVault vault = new("LG AC Test " + Guid.NewGuid());

    [TestCleanup]
    public void Cleanup()
    {
        vault.Delete();
    }

    [TestMethod]
    public void ReadIsNullWhenMissing()
    {
        Assert.IsNull(vault.Read());
    }

    [TestMethod]
    public void SaveThenReadRoundTrips()
    {
        vault.Save("fake-token-123");

        Assert.AreEqual("fake-token-123", vault.Read());
    }

    [TestMethod]
    public void SaveOverwrites()
    {
        vault.Save("first-fake-token");
        vault.Save("second-fake-token");

        Assert.AreEqual("second-fake-token", vault.Read());
    }

    [TestMethod]
    public void LongTokenRoundTrips()
    {
        var token = new string('a', 2000);

        vault.Save(token);

        Assert.AreEqual(token, vault.Read());
    }

    [TestMethod]
    public void DeleteTwiceDoesNotThrow()
    {
        vault.Save("fake-token-123");

        vault.Delete();
        vault.Delete();

        Assert.IsNull(vault.Read());
    }
}
