using LGAC.Linux.Platform;
using LGAC.Linux.Tests.Support;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class SecretServiceVaultTests
{
    [TestMethod]
    public void MissingTokenReadsNull()
    {
        Assert.IsNull(new SecretServiceVault(new FakeSecretService()).Read());
    }

    [TestMethod]
    public void RoundTripsWithLabelAndAttributes()
    {
        var service = new FakeSecretService();
        var vault = new SecretServiceVault(service);

        vault.Save("token-1");

        Assert.AreEqual("token-1", vault.Read());
        Assert.AreEqual("LG AC", service.LastLabel);
        Assert.AreEqual("lg-ac", service.LastAttributes!["application"]);
        Assert.AreEqual("thinq-token", service.LastAttributes["kind"]);
    }

    [TestMethod]
    public void SaveOverwrites()
    {
        var service = new FakeSecretService();
        var vault = new SecretServiceVault(service);

        vault.Save("old");
        vault.Save("new");

        Assert.AreEqual("new", vault.Read());
        Assert.AreEqual(1, service.ItemCount);
    }

    [TestMethod]
    public void DeleteRemovesToken()
    {
        var service = new FakeSecretService();
        var vault = new SecretServiceVault(service);
        vault.Save("token");

        vault.Delete();

        Assert.IsNull(vault.Read());
        Assert.AreEqual(0, service.ItemCount);
    }

    [TestMethod]
    public void ServiceFailuresPropagate()
    {
        var service = new FakeSecretService { FailLookup = true, FailStore = true, FailClear = true };
        var vault = new SecretServiceVault(service);

        Assert.ThrowsExactly<SecretServiceException>(() => vault.Read());
        Assert.ThrowsExactly<SecretServiceException>(() => vault.Save("token"));
        Assert.ThrowsExactly<SecretServiceException>(() => vault.Delete());
    }
}
