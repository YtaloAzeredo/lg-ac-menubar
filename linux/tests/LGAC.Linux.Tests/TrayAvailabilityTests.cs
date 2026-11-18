using LGAC.Linux.Platform;
using LGAC.Linux.Tests.Support;
using Tmds.DBus.Protocol;

namespace LGAC.Linux.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TrayAvailabilityTests
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    [TestMethod]
    public async Task ReturnsWithoutThrowing()
    {
        DBusTestEnvironment.RequireSessionBus();

        _ = await TrayAvailability.IsAvailableAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task UnreachableBusMeansNoTray()
    {
        Assert.IsFalse(await TrayAvailability.IsAvailableAsync(TimeSpan.FromSeconds(2), DBusTestEnvironment.UnreachableAddress));
    }

    [TestMethod]
    public async Task ReturnsTrueWhenAHostIsRegistered()
    {
        DBusTestEnvironment.RequireSessionBus();

        Assert.IsTrue(await ProbeWithWatcherAsync(true));
    }

    [TestMethod]
    public async Task ReturnsFalseWhenNoHostIsRegistered()
    {
        DBusTestEnvironment.RequireSessionBus();

        Assert.IsFalse(await ProbeWithWatcherAsync(false));
    }

    [TestMethod]
    public async Task ExpectedTrayThatRegistersOnThirdProbeIsFound()
    {
        var probes = 0;
        var pauses = new List<TimeSpan>();

        var result = await TrayAvailability.WaitAsync(
            () => Task.FromResult(++probes == 3),
            true,
            Limit,
            Interval,
            pause =>
            {
                pauses.Add(pause);
                return Task.CompletedTask;
            });

        Assert.IsTrue(result);
        Assert.AreEqual(3, probes);
        Assert.HasCount(2, pauses);
    }

    [TestMethod]
    public async Task ExpectedTrayThatNeverRegistersGivesUpAfterTheLimit()
    {
        var probes = 0;
        var waited = TimeSpan.Zero;

        var result = await TrayAvailability.WaitAsync(
            () =>
            {
                probes++;
                return Task.FromResult(false);
            },
            true,
            Limit,
            Interval,
            pause =>
            {
                waited += pause;
                return Task.CompletedTask;
            });

        Assert.IsFalse(result);
        Assert.AreEqual(Limit, waited);
        Assert.AreEqual(31, probes);
    }

    [TestMethod]
    public async Task UnexpectedTrayProbesOnlyOnce()
    {
        var probes = 0;
        var pauses = 0;

        var result = await TrayAvailability.WaitAsync(
            () =>
            {
                probes++;
                return Task.FromResult(false);
            },
            false,
            Limit,
            Interval,
            _ =>
            {
                pauses++;
                return Task.CompletedTask;
            });

        Assert.IsFalse(result);
        Assert.AreEqual(1, probes);
        Assert.AreEqual(0, pauses);
    }

    [TestMethod]
    public async Task AvailableTrayReturnsOnTheFirstProbe()
    {
        var probes = 0;

        var result = await TrayAvailability.WaitAsync(
            () =>
            {
                probes++;
                return Task.FromResult(true);
            },
            true,
            Limit,
            Interval,
            _ => Task.CompletedTask);

        Assert.IsTrue(result);
        Assert.AreEqual(1, probes);
    }

    [TestMethod]
    public async Task CancelledWaitGivesUp()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await TrayAvailability.WaitAsync(
            () => Task.FromResult(false),
            true,
            Limit,
            Interval,
            cancellationToken: cancellation.Token);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("KDE", true)]
    [DataRow("XFCE", true)]
    [DataRow("X-Cinnamon", true)]
    [DataRow("MATE", true)]
    [DataRow("LXQt", true)]
    [DataRow("Budgie:GNOME", true)]
    [DataRow("Unity", true)]
    [DataRow("kde", true)]
    [DataRow("ubuntu:GNOME", false)]
    [DataRow("GNOME", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void ExpectsTrayFollowsTheDesktop(string? desktop, bool expected)
    {
        Assert.AreEqual(expected, TrayAvailability.ExpectsTray(desktop));
    }

    private static async Task<bool> ProbeWithWatcherAsync(bool hostRegistered)
    {
        using var watcher = new DBusConnection(DBusTestEnvironment.SessionAddress!);
        await watcher.ConnectAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        watcher.AddMethodHandler(new FakeWatcher(hostRegistered));
        await watcher.RequestNameAsync(WatcherName, RequestNameOptions.None).WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            return await TrayAvailability.IsAvailableAsync(TimeSpan.FromSeconds(5), DBusTestEnvironment.SessionAddress);
        }
        finally
        {
            await watcher.ReleaseNameAsync(WatcherName).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class FakeWatcher(bool hostRegistered) : IPathMethodHandler
    {
        public string Path => WatcherPath;

        public bool HandlesChildPaths => false;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            var request = context.Request;
            if (request.InterfaceAsString == "org.freedesktop.DBus.Properties" && request.MemberAsString == "Get")
            {
                var reader = request.GetBodyReader();
                var requestedInterface = reader.ReadString();
                var property = reader.ReadString();
                if (requestedInterface == WatcherName && property == "IsStatusNotifierHostRegistered")
                {
                    using var writer = context.CreateReplyWriter("v");
                    writer.WriteVariantBool(hostRegistered);
                    context.Reply(writer.CreateMessage());
                    return ValueTask.CompletedTask;
                }
            }

            context.ReplyUnknownMethodError();
            return ValueTask.CompletedTask;
        }
    }
}
