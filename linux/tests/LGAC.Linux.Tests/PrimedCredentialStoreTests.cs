using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public class PrimedCredentialStoreTests
{
    [TestMethod]
    public void FirstReadUsesThePrimedValueWithoutAskingTheInnerStore()
    {
        var inner = InMemoryCredentialStore.Configured();
        var primed = new PrimedCredentialStore(inner);
        primed.Prime();
        var accesses = inner.VaultAccesses;

        Assert.AreEqual("tok", primed.Token);
        Assert.AreEqual(accesses, inner.VaultAccesses);
        Assert.AreEqual("tok", primed.Token);
        Assert.IsGreaterThan(accesses, inner.VaultAccesses);
    }

    [TestMethod]
    public void WithoutPrimingEveryReadGoesToTheInnerStore()
    {
        var inner = InMemoryCredentialStore.Configured();
        var primed = new PrimedCredentialStore(inner);

        Assert.AreEqual("tok", primed.Token);
        Assert.AreEqual(1, inner.VaultAccesses);
    }

    [TestMethod]
    public void FailureWhilePrimingMeansNoToken()
    {
        var inner = InMemoryCredentialStore.Configured();
        inner.ReadError = new InvalidOperationException("cofre bloqueado");
        var primed = new PrimedCredentialStore(inner);

        primed.Prime();

        Assert.IsNull(primed.Token);
    }

    [TestMethod]
    public void SaveAndDeleteInvalidateThePrimedValue()
    {
        var inner = InMemoryCredentialStore.Configured();
        var primed = new PrimedCredentialStore(inner);
        primed.Prime();
        primed.SaveToken("new");
        Assert.AreEqual("new", primed.Token);

        var other = new PrimedCredentialStore(inner);
        other.Prime();
        other.DeleteToken();
        Assert.IsNull(other.Token);
    }

    [TestMethod]
    public void OtherMembersDelegate()
    {
        var inner = InMemoryCredentialStore.Configured();
        ICredentialStore primed = new PrimedCredentialStore(inner);

        primed.Country = "US";
        primed.DeviceId = "x";
        primed.DeviceAlias = "y";

        Assert.AreEqual("US", inner.Country);
        Assert.AreEqual("x", inner.DeviceId);
        Assert.AreEqual("y", inner.DeviceAlias);
        Assert.AreEqual(inner.ClientId, primed.ClientId);
    }
}
