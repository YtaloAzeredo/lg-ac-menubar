using System.Diagnostics;
using LGAC.Linux.Platform;
using LGAC.Linux.Tests.Support;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class DBusUnavailableTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly IReadOnlyDictionary<string, string> Attributes = new Dictionary<string, string> { ["application"] = "lg-ac-test" };

    private static DBusSecretService CreateService() =>
        new(Timeout, TimeSpan.FromSeconds(60), DBusTestEnvironment.UnreachableAddress);

    [TestMethod]
    public void IsAvailableIsFalseQuicklyWithoutBus()
    {
        var clock = Stopwatch.StartNew();

        Assert.IsFalse(CreateService().IsAvailable());

        Assert.IsLessThan(Timeout + TimeSpan.FromSeconds(2), clock.Elapsed);
    }

    [TestMethod]
    public void OperationsThrowSecretServiceExceptionWithoutBus()
    {
        var service = CreateService();

        Assert.ThrowsExactly<SecretServiceException>(() => service.Lookup(Attributes));
        Assert.ThrowsExactly<SecretServiceException>(() => service.Store("LG AC", Attributes, "segredo-confidencial"));
        Assert.ThrowsExactly<SecretServiceException>(() => service.Clear(Attributes));
    }

    [TestMethod]
    public void ExceptionMessageDoesNotLeakSecret()
    {
        var exception = Assert.ThrowsExactly<SecretServiceException>(
            () => CreateService().Store("LG AC", Attributes, "segredo-confidencial"));

        Assert.DoesNotContain("segredo-confidencial", exception.Message);
    }

    [TestMethod]
    public void MissingBusAddressBehavesAsUnavailable()
    {
        var service = new DBusSecretService(Timeout, TimeSpan.FromSeconds(60), "");

        Assert.IsFalse(service.IsAvailable());
        Assert.ThrowsExactly<SecretServiceException>(() => service.Lookup(Attributes));
    }

    [TestMethod]
    public void MalformedBusAddressBehavesAsUnavailable()
    {
        var service = new DBusSecretService(Timeout, TimeSpan.FromSeconds(60), "isto não é um endereço");

        Assert.IsFalse(service.IsAvailable());
        Assert.ThrowsExactly<SecretServiceException>(() => service.Lookup(Attributes));
    }

    [TestMethod]
    public async Task TrayAvailabilityIsFalseWithoutBus()
    {
        Assert.IsFalse(await TrayAvailability.IsAvailableAsync(Timeout, DBusTestEnvironment.UnreachableAddress));
        Assert.IsFalse(await TrayAvailability.IsAvailableAsync(Timeout, ""));
        Assert.IsFalse(await TrayAvailability.IsAvailableAsync(Timeout, "isto não é um endereço"));
    }

    [TestMethod]
    public async Task ResumeWatcherStartCompletesWithoutBus()
    {
        var resumed = 0;
        using var watcher = new ResumeWatcher(() => resumed++, DBusTestEnvironment.UnreachableAddress);

        await watcher.Start().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(0, resumed);
    }

    [TestMethod]
    public void ResumeWatcherDisposeIsSafe()
    {
        var never = new ResumeWatcher(() => { }, DBusTestEnvironment.UnreachableAddress);
        never.Dispose();
        never.Dispose();
    }

    [TestMethod]
    public async Task ResumeWatcherDisposeAfterStartIsSafe()
    {
        var watcher = new ResumeWatcher(() => { }, DBusTestEnvironment.UnreachableAddress);
        await watcher.Start();

        watcher.Dispose();
        watcher.Dispose();
    }
}
