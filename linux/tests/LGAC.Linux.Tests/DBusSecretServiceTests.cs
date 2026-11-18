using LGAC.Linux.Platform;
using LGAC.Linux.Tests.Support;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class DBusSecretServiceTests
{
    private DBusSecretService service = null!;
    private Dictionary<string, string> attributes = null!;

    [TestInitialize]
    public void Setup()
    {
        service = DBusTestEnvironment.RequireSecretService();

        attributes = new Dictionary<string, string>
        {
            ["application"] = "lg-ac-test",
            ["run"] = Guid.NewGuid().ToString("N"),
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (service is not null && attributes is not null)
        {
            service.Clear(attributes);
        }
    }

    [TestMethod]
    public void RoundTrips()
    {
        service.Store("LG AC teste", attributes, "token-de-teste");

        Assert.AreEqual("token-de-teste", service.Lookup(attributes));
    }

    [TestMethod]
    public void StoreOverwritesWithSingleItem()
    {
        service.Store("LG AC teste", attributes, "antigo");
        service.Store("LG AC teste", attributes, "novo");

        Assert.AreEqual("novo", service.Lookup(attributes));
        Assert.AreEqual(1, service.CountItems(attributes));
        service.Clear(attributes);
        Assert.IsNull(service.Lookup(attributes));
        Assert.AreEqual(0, service.CountItems(attributes));
    }

    [TestMethod]
    public void StoresLongToken()
    {
        var token = new string('a', 2000);

        service.Store("LG AC teste", attributes, token);

        Assert.AreEqual(token, service.Lookup(attributes));
    }

    [TestMethod]
    public void StoresAccentedCharacters()
    {
        const string token = "açaí-ñandú-日本-😀";

        service.Store("LG AC teste", attributes, token);

        Assert.AreEqual(token, service.Lookup(attributes));
    }

    [TestMethod]
    public void LookupMissingReturnsNull()
    {
        Assert.IsNull(service.Lookup(attributes));
    }

    [TestMethod]
    public void ClearTwiceDoesNotThrow()
    {
        service.Store("LG AC teste", attributes, "token");

        service.Clear(attributes);
        service.Clear(attributes);

        Assert.IsNull(service.Lookup(attributes));
    }
}
