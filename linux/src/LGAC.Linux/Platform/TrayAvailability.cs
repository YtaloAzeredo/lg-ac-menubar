using Tmds.DBus.Protocol;

namespace LGAC.Linux.Platform;

public static class TrayAvailability
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";
    private const string HostRegisteredProperty = "IsStatusNotifierHostRegistered";

    private static readonly string[] TrayDesktops = ["KDE", "XFCE", "X-Cinnamon", "MATE", "LXQt", "Budgie", "Unity"];

    public static bool ExpectsTray(string? currentDesktop)
    {
        if (string.IsNullOrEmpty(currentDesktop))
        {
            return false;
        }

        foreach (var entry in currentDesktop.Split(':'))
        {
            foreach (var known in TrayDesktops)
            {
                if (string.Equals(entry.Trim(), known, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static async Task<bool> WaitAsync(
        Func<Task<bool>> probe,
        bool expectTray,
        TimeSpan limit,
        TimeSpan interval,
        Func<TimeSpan, Task>? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (await probe().ConfigureAwait(false))
        {
            return true;
        }

        if (!expectTray)
        {
            return false;
        }

        var wait = delay ?? (pause => Task.Delay(pause, cancellationToken));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var waited = TimeSpan.Zero;
        while (waited < limit && clock.Elapsed < limit)
        {
            try
            {
                await wait(interval).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            waited += interval;
            if (await probe().ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<bool> IsAvailableAsync(TimeSpan timeout, string? busAddress = null)
    {
        var address = busAddress ?? DBusAddress.Session;
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }

        DBusConnection? connection = null;
        try
        {
            connection = new DBusConnection(address);
            var work = Task.Run(async () =>
            {
                await DBusBus.ConnectAsync(connection, timeout).ConfigureAwait(false);
                if (!await DBusBus.NameHasOwnerAsync(connection, WatcherName).ConfigureAwait(false))
                {
                    return false;
                }

                return await DBusBus.GetBoolPropertyAsync(connection, WatcherName, WatcherPath, WatcherName, HostRegisteredProperty).ConfigureAwait(false);
            });
            DBusBus.Observe(work);
            return await work.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            connection?.Dispose();
        }
    }
}
