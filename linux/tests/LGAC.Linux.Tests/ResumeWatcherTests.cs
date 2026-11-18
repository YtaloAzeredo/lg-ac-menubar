using LGAC.Linux.Platform;
using LGAC.Linux.Tests.Support;
using Tmds.DBus.Protocol;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class ResumeWatcherTests
{
    [TestMethod]
    public async Task StartsAndDisposesWithoutThrowing()
    {
        DBusTestEnvironment.RequireSessionBus();

        using var watcher = new ResumeWatcher(() => { }, DBusTestEnvironment.SessionAddress);
        await watcher.Start().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task CallsOnResumeOnlyWhenWakingUp()
    {
        DBusTestEnvironment.RequireSessionBus();
        var resumed = 0;
        var woke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new ResumeWatcher(
            () =>
            {
                Interlocked.Increment(ref resumed);
                woke.TrySetResult();
            },
            DBusTestEnvironment.SessionAddress);
        await watcher.Start().WaitAsync(TimeSpan.FromSeconds(10));

        using var sender = new DBusConnection(DBusTestEnvironment.SessionAddress!);
        await sender.ConnectAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(sender.TrySendMessage(PrepareForSleep(sender, true)));
        Assert.IsTrue(sender.TrySendMessage(PrepareForSleep(sender, false)));

        await woke.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(1, Volatile.Read(ref resumed));
    }

    private static MessageBuffer PrepareForSleep(DBusConnection connection, bool sleeping)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteSignalHeader(
                path: "/org/freedesktop/login1",
                @interface: "org.freedesktop.login1.Manager",
                member: "PrepareForSleep",
                signature: "b");
            writer.WriteBool(sleeping);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }
}
