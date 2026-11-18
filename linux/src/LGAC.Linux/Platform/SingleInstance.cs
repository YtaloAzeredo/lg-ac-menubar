using System.Net.Sockets;
using System.Text;

namespace LGAC.Linux.Platform;

internal sealed class SingleInstance : IDisposable
{
    private const int MaxSocketPathBytes = 100;

    private readonly FileStream lockStream;
    private readonly Action onActivate;
    private readonly string socketFile;
    private readonly CancellationTokenSource cancellation = new();
    private Socket? listener;
    private Task? acceptLoop;
    private bool disposed;

    private SingleInstance(FileStream lockStream, string socketFile, Action onActivate)
    {
        this.lockStream = lockStream;
        this.socketFile = socketFile;
        this.onActivate = onActivate;
    }

    public static SingleInstance? TryAcquire(string lockFile, string socketFile, Action onActivate)
    {
        FileStream stream;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(lockFile));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            stream = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }

        var instance = new SingleInstance(stream, socketFile, onActivate);
        instance.StartListening();
        return instance;
    }

    public static bool SignalExisting(string socketFile, TimeSpan timeout)
    {
        try
        {
            if (!FitsSocketPath(socketFile))
            {
                return false;
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                var remaining = timeout - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return false;
                }

                if (TryConnect(socketFile, remaining))
                {
                    return true;
                }

                var pause = TimeSpan.FromMilliseconds(100);
                remaining = timeout - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return false;
                }

                Thread.Sleep(remaining < pause ? remaining : pause);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryConnect(string socketFile, TimeSpan limit)
    {
        try
        {
            using var cancel = new CancellationTokenSource(limit);
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ConnectAsync(new UnixDomainSocketEndPoint(socketFile), cancel.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            cancellation.Cancel();
        }
        catch (Exception)
        {
        }

        CloseListener();
        if (acceptLoop is not null)
        {
            DBusBus.Observe(acceptLoop);
        }

        if (listener is not null)
        {
            try
            {
                File.Delete(socketFile);
            }
            catch (Exception)
            {
            }
        }

        try
        {
            lockStream.Dispose();
        }
        catch (Exception)
        {
        }

        cancellation.Dispose();
    }

    private static bool FitsSocketPath(string socketFile)
    {
        return Encoding.UTF8.GetByteCount(socketFile) < MaxSocketPathBytes;
    }

    private void StartListening()
    {
        if (!FitsSocketPath(socketFile))
        {
            return;
        }

        Socket? created = null;
        try
        {
            if (File.Exists(socketFile))
            {
                File.Delete(socketFile);
            }

            created = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            created.Bind(new UnixDomainSocketEndPoint(socketFile));
            created.Listen(8);
            TryRestrictSocket();
        }
        catch (Exception)
        {
            created?.Dispose();
            return;
        }

        listener = created;
        acceptLoop = Task.Run(() => AcceptLoopAsync(created, cancellation.Token));
    }

    private void TryRestrictSocket()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(socketFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
        }
    }

    private void CloseListener()
    {
        try
        {
            listener?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private async Task AcceptLoopAsync(Socket socket, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var client = await socket.AcceptAsync(token);
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), token);
                }
                catch (Exception)
                {
                    return;
                }

                continue;
            }

            try
            {
                onActivate();
            }
            catch (Exception)
            {
            }
        }
    }
}
