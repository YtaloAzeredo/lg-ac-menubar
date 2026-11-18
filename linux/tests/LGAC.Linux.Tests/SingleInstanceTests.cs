using System.Net.Sockets;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public class SingleInstanceTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lgsi" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Cleanup(string directory)
    {
        try
        {
            Directory.Delete(directory, true);
        }
        catch (IOException)
        {
        }
    }

    [TestMethod]
    public void SecondAcquisitionOnTheSamePathsReturnsNull()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            using var first = SingleInstance.TryAcquire(lockFile, socketFile, () => { });
            Assert.IsNotNull(first);
            Assert.IsNull(SingleInstance.TryAcquire(lockFile, socketFile, () => { }));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void SignalExistingTriggersOnActivate()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            using var activated = new SemaphoreSlim(0);
            using var first = SingleInstance.TryAcquire(lockFile, socketFile, () => activated.Release());
            Assert.IsNotNull(first);

            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, Wait));
            Assert.IsTrue(activated.Wait(Wait));
            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, Wait));
            Assert.IsTrue(activated.Wait(Wait));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void SignalExistingWithoutListenerReturnsFalse()
    {
        var directory = NewDirectory();
        try
        {
            Assert.IsFalse(SingleInstance.SignalExisting(Path.Combine(directory, "none.sock"), TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void DisposeAllowsAcquiringAgainAndRemovesTheSocket()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            var first = SingleInstance.TryAcquire(lockFile, socketFile, () => { });
            Assert.IsNotNull(first);
            Assert.IsTrue(File.Exists(socketFile));
            first.Dispose();
            first.Dispose();
            Assert.IsFalse(File.Exists(socketFile));
            Assert.IsFalse(SingleInstance.SignalExisting(socketFile, TimeSpan.FromMilliseconds(500)));

            using var second = SingleInstance.TryAcquire(lockFile, socketFile, () => { });
            Assert.IsNotNull(second);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void OrphanSocketFileDoesNotPreventAcquiring()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            File.WriteAllText(socketFile, "orphan");
            using var activated = new SemaphoreSlim(0);
            using var instance = SingleInstance.TryAcquire(lockFile, socketFile, () => activated.Release());
            Assert.IsNotNull(instance);
            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, Wait));
            Assert.IsTrue(activated.Wait(Wait));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void TooLongSocketPathDoesNotThrowAndTheLockStillHolds()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, new string('s', 200) + ".sock");
            using var first = SingleInstance.TryAcquire(lockFile, socketFile, () => { });
            Assert.IsNotNull(first);
            Assert.IsNull(SingleInstance.TryAcquire(lockFile, socketFile, () => { }));
            Assert.IsFalse(SingleInstance.SignalExisting(socketFile, TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void ThrowingActivationCallbackDoesNotStopTheListener()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            using var activated = new SemaphoreSlim(0);
            var calls = 0;
            using var instance = SingleInstance.TryAcquire(lockFile, socketFile, () =>
            {
                activated.Release();
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("falha simulada");
                }
            });
            Assert.IsNotNull(instance);
            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, Wait));
            Assert.IsTrue(activated.Wait(Wait));
            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, Wait));
            Assert.IsTrue(activated.Wait(Wait));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void SignalExistingWaitsForAListenerThatStartsLate()
    {
        var directory = NewDirectory();
        SingleInstance? late = null;
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            using var activated = new SemaphoreSlim(0);
            var starter = Task.Run(async () =>
            {
                await Task.Delay(300);
                late = SingleInstance.TryAcquire(lockFile, socketFile, () => activated.Release());
            });

            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, TimeSpan.FromSeconds(3)));
            starter.Wait(Wait);
            Assert.IsNotNull(late);
            Assert.IsTrue(activated.Wait(Wait));
        }
        finally
        {
            late?.Dispose();
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void SignalExistingGivesUpAtTheLimitWhenNobodyListens()
    {
        var directory = NewDirectory();
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Assert.IsFalse(SingleInstance.SignalExisting(Path.Combine(directory, "none.sock"), TimeSpan.FromMilliseconds(600)));
            Assert.IsGreaterThanOrEqualTo(500, clock.ElapsedMilliseconds);
            Assert.IsLessThan(3000, clock.ElapsedMilliseconds);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void RealOrphanSocketFileDoesNotPreventAcquiring()
    {
        var directory = NewDirectory();
        try
        {
            var lockFile = Path.Combine(directory, "a.lock");
            var socketFile = Path.Combine(directory, "a.sock");
            using (var orphan = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                orphan.Bind(new UnixDomainSocketEndPoint(socketFile));
                orphan.Listen(1);
                File.Move(socketFile, socketFile + ".kept");
            }

            File.Move(socketFile + ".kept", socketFile);

            Assert.IsTrue(File.Exists(socketFile));
            Assert.IsFalse(SingleInstance.SignalExisting(socketFile, TimeSpan.FromMilliseconds(300)));
            using var activated = new SemaphoreSlim(0);
            using var instance = SingleInstance.TryAcquire(lockFile, socketFile, () => activated.Release());
            Assert.IsNotNull(instance);
            Assert.IsTrue(SingleInstance.SignalExisting(socketFile, Wait));
            Assert.IsTrue(activated.Wait(Wait));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [TestMethod]
    public void SocketFileIsOwnerOnly()
    {
        var directory = NewDirectory();
        try
        {
            var socketFile = Path.Combine(directory, "a.sock");
            using var instance = SingleInstance.TryAcquire(Path.Combine(directory, "a.lock"), socketFile, () => { });
            Assert.IsNotNull(instance);
            if (!OperatingSystem.IsWindows())
            {
                Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(socketFile));
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }
}
