using LGAC.Core.Tests.Support;
using LGAC.Linux.Platform;
using LGAC.Linux.Tests.Support;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class FallbackTokenVaultTests
{
    private readonly FakeSecretService service = new();
    private readonly InMemoryTokenVault file = new();

    private FallbackTokenVault Create() => new(service, file);

    [TestMethod]
    public void SaveWithServiceUsesServiceAndClearsFile()
    {
        file.Save("legacy");
        var vault = Create();

        vault.Save("token");

        Assert.AreEqual("token", new SecretServiceVault(service).Read());
        Assert.IsNull(file.Read());
    }

    [TestMethod]
    public void SaveWithoutServiceUsesFile()
    {
        service.Available = false;
        var vault = Create();

        vault.Save("token");

        Assert.AreEqual("token", file.Read());
        Assert.AreEqual(0, service.ItemCount);
    }

    [TestMethod]
    public void ReadFallsBackToFileWhenServiceHasNothing()
    {
        file.Save("legacy");

        Assert.AreEqual("legacy", Create().Read());
    }

    [TestMethod]
    public void ReadPrefersService()
    {
        file.Save("legacy");
        new SecretServiceVault(service).Save("token");

        Assert.AreEqual("token", Create().Read());
    }

    [TestMethod]
    public void ReadFallsBackToFileWhenServiceThrows()
    {
        file.Save("legacy");
        service.FailLookup = true;

        Assert.AreEqual("legacy", Create().Read());
    }

    [TestMethod]
    public void ReadWithoutServiceReadsFile()
    {
        service.Available = false;
        file.Save("legacy");

        Assert.AreEqual("legacy", Create().Read());
    }

    [TestMethod]
    public void SaveRefusedByServiceThrows()
    {
        service.FailStore = true;
        file.Save("legacy");
        var vault = Create();

        var exception = Assert.ThrowsExactly<SecretServiceException>(() => vault.Save("token"));

        Assert.IsFalse(exception.NoVault);
        Assert.AreEqual("legacy", file.Read());
    }

    [TestMethod]
    public void SaveFallsBackToFileWhenServiceHasNoVault()
    {
        service.FailStoreWithoutVault = true;
        var vault = Create();

        vault.Save("token");

        Assert.AreEqual("token", file.Read());
    }

    [TestMethod]
    public void AvailabilityFailureBehavesAsUnavailable()
    {
        service.FailAvailability = true;
        file.Save("legacy");
        var vault = Create();

        Assert.AreEqual("legacy", vault.Read());
        vault.Save("novo");
        Assert.AreEqual("novo", file.Read());
        vault.Delete();
        Assert.IsNull(file.Read());
    }

    [TestMethod]
    public void SaveSucceedsWhenFileDeleteThrows()
    {
        var vault = new FallbackTokenVault(service, new ThrowingDeleteVault());

        vault.Save("token");

        Assert.AreEqual("token", new SecretServiceVault(service).Read());
    }

    private sealed class ThrowingDeleteVault : LGAC.Core.ITokenVault
    {
        public string? Read() => null;

        public void Save(string token)
        {
        }

        public void Delete() => throw new IOException("falha");
    }

    [TestMethod]
    public void DeleteClearsBothAndSwallowsServiceErrors()
    {
        new SecretServiceVault(service).Save("token");
        file.Save("legacy");

        Create().Delete();

        Assert.AreEqual(0, service.ItemCount);
        Assert.IsNull(file.Read());

        service.FailClear = true;
        file.Save("legacy");
        Create().Delete();
        Assert.IsNull(file.Read());
    }
}
