using Tmds.DBus.Protocol;

namespace LGAC.Linux.Platform;

public sealed class ResumeWatcher(Action onResume, string? busAddress = null) : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private DBusConnection? connection;
    private IDisposable? subscription;
    private bool disposed;

    public Task Start() => Task.Run(StartCoreAsync);

    public void Dispose()
    {
        DBusConnection? toDispose;
        IDisposable? toUnsubscribe;
        lock (gate)
        {
            disposed = true;
            toDispose = connection;
            toUnsubscribe = subscription;
            connection = null;
            subscription = null;
        }

        Release(toUnsubscribe, toDispose);
    }

    private async Task StartCoreAsync()
    {
        var address = busAddress ?? DBusAddress.System;
        if (string.IsNullOrEmpty(address))
        {
            return;
        }

        DBusConnection? created = null;
        IDisposable? pendingSubscription = null;
        try
        {
            lock (gate)
            {
                if (disposed || connection is not null)
                {
                    return;
                }

                created = new DBusConnection(address);
                connection = created;
            }

            await DBusBus.ConnectAsync(created, ConnectTimeout).ConfigureAwait(false);

            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Path = "/org/freedesktop/login1",
                Interface = "org.freedesktop.login1.Manager",
                Member = "PrepareForSleep",
            };
            MessageValueReader<bool> reader = static (message, state) =>
            {
                var body = message.GetBodyReader();
                return body.ReadBool();
            };
            pendingSubscription = await created
                .AddMatchAsync(rule, reader, OnSignal, emitOnCapturedContext: false, flags: ObserverFlags.None, state: null)
                .AsTask()
                .WaitAsync(ConnectTimeout)
                .ConfigureAwait(false);

            var discard = false;
            lock (gate)
            {
                if (disposed || !ReferenceEquals(connection, created))
                {
                    discard = true;
                }
                else
                {
                    subscription = pendingSubscription;
                }
            }

            if (discard)
            {
                Release(pendingSubscription, created);
            }
        }
        catch (Exception)
        {
            lock (gate)
            {
                if (ReferenceEquals(connection, created))
                {
                    connection = null;
                }
            }

            Release(pendingSubscription, created);
        }
    }

    private void OnSignal(Notification<bool> notification)
    {
        if (!notification.HasValue || notification.Value)
        {
            return;
        }

        try
        {
            onResume();
        }
        catch (Exception)
        {
        }
    }

    private static void Release(IDisposable? toUnsubscribe, DBusConnection? toDispose)
    {
        try
        {
            toUnsubscribe?.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            toDispose?.Dispose();
        }
        catch (Exception)
        {
        }
    }
}
