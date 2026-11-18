using Tmds.DBus.Protocol;

namespace LGAC.Linux.Platform;

internal delegate void BodyWriter(ref MessageWriter writer);

internal static class DBusBus
{
    private const string BusName = "org.freedesktop.DBus";
    private const string BusPath = "/org/freedesktop/DBus";

    public static MessageBuffer BuildCall(
        DBusConnection connection,
        string destination,
        string path,
        string @interface,
        string member,
        string? signature = null,
        BodyWriter? body = null)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(
                destination: destination,
                path: path,
                @interface: @interface,
                signature: signature,
                member: member);
            body?.Invoke(ref writer);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }

    public static async Task ConnectAsync(DBusConnection connection, TimeSpan timeout)
    {
        await connection.ConnectAsync().AsTask().WaitAsync(timeout).ConfigureAwait(false);
    }

    public static Task<bool> NameHasOwnerAsync(DBusConnection connection, string name)
    {
        var message = BuildCall(
            connection,
            BusName,
            BusPath,
            BusName,
            "NameHasOwner",
            "s",
            (ref MessageWriter writer) => writer.WriteString(name));
        return connection.CallMethodAsync(
            message,
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                return reader.ReadBool();
            },
            null);
    }

    public static Task<bool> GetBoolPropertyAsync(
        DBusConnection connection,
        string destination,
        string path,
        string @interface,
        string property)
    {
        var message = BuildCall(
            connection,
            destination,
            path,
            "org.freedesktop.DBus.Properties",
            "Get",
            "ss",
            (ref MessageWriter writer) =>
            {
                writer.WriteString(@interface);
                writer.WriteString(property);
            });
        return connection.CallMethodAsync(
            message,
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                return reader.ReadVariantValue().GetBool();
            },
            null);
    }

    public static void Observe(Task task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
