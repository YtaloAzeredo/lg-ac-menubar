using System.Text;
using Tmds.DBus.Protocol;

namespace LGAC.Linux.Platform;

public sealed class DBusSecretService(TimeSpan timeout, TimeSpan promptTimeout, string? busAddress = null) : ISecretService
{
    private const string ServiceName = "org.freedesktop.secrets";
    private const string ServicePath = "/org/freedesktop/secrets";
    private const string ServiceInterface = "org.freedesktop.Secret.Service";
    private const string CollectionInterface = "org.freedesktop.Secret.Collection";
    private const string ItemInterface = "org.freedesktop.Secret.Item";
    private const string PromptInterface = "org.freedesktop.Secret.Prompt";
    private const string SessionInterface = "org.freedesktop.Secret.Session";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    private const string LabelProperty = "org.freedesktop.Secret.Item.Label";
    private const string AttributesProperty = "org.freedesktop.Secret.Item.Attributes";
    private const string NoObject = "/";
    private const string LoginCollectionPath = "/org/freedesktop/secrets/collection/login";
    private const string SessionCollectionPath = "/org/freedesktop/secrets/collection/session";
    private static readonly TimeSpan DismissTimeout = TimeSpan.FromSeconds(2);
    private const string ContentType = "text/plain; charset=utf8";

    private readonly string? address = busAddress ?? DBusAddress.Session;
    private volatile bool knownAvailable;

    public bool IsAvailable()
    {
        if (knownAvailable)
        {
            return true;
        }

        try
        {
            var available = Run(async connection =>
            {
                if (await Step(DBusBus.NameHasOwnerAsync(connection, ServiceName)))
                {
                    return true;
                }

                var activatable = await Step(connection.ListActivatableServicesAsync());
                return Array.IndexOf(activatable, ServiceName) >= 0;
            });
            knownAvailable = available;
            return available;
        }
        catch (SecretServiceException)
        {
            return false;
        }
    }

    public string? Lookup(IReadOnlyDictionary<string, string> attributes)
    {
        return Run(async connection =>
        {
            var (unlocked, locked) = await SearchAsync(connection, attributes);
            if (unlocked.Length == 0 && locked.Length == 0)
            {
                return null;
            }

            var session = await OpenSessionAsync(connection);
            try
            {
                var item = unlocked.Length > 0 ? unlocked[0] : locked[0];
                if (unlocked.Length == 0)
                {
                    await UnlockAsync(connection, [item]);
                }

                var bytes = await Step(connection.CallMethodAsync(
                    DBusBus.BuildCall(
                        connection,
                        ServiceName,
                        item,
                        ItemInterface,
                        "GetSecret",
                        "o",
                        (ref MessageWriter writer) => writer.WriteObjectPath(session)),
                    static (Message reply, object? state) =>
                    {
                        var reader = reply.GetBodyReader();
                        reader.AlignStruct();
                        reader.ReadObjectPathAsString();
                        reader.ReadArrayOfByte();
                        var value = reader.ReadArrayOfByte();
                        reader.ReadString();
                        return value;
                    },
                    null));
                try
                {
                    return Encoding.UTF8.GetString(bytes);
                }
                finally
                {
                    Array.Clear(bytes);
                }
            }
            finally
            {
                await CloseSessionAsync(connection, session);
            }
        });
    }

    public void Store(string label, IReadOnlyDictionary<string, string> attributes, string secret)
    {
        Run<object?>(async connection =>
        {
            var collection = await ResolveCollectionAsync(connection)
                ?? throw new SecretServiceException("O serviço de segredos não tem um cofre disponível para gravar.", noVault: true);
            await UnlockAsync(connection, [collection]);

            var session = await OpenSessionAsync(connection);
            var bytes = Encoding.UTF8.GetBytes(secret);
            try
            {
                var properties = new Dictionary<string, VariantValue>
                {
                    [LabelProperty] = label,
                    [AttributesProperty] = new Dict<string, string>(attributes.ToDictionary(static pair => pair.Key, static pair => pair.Value)),
                };
                var prompt = await Step(connection.CallMethodAsync(
                    DBusBus.BuildCall(
                        connection,
                        ServiceName,
                        collection,
                        CollectionInterface,
                        "CreateItem",
                        "a{sv}(oayays)b",
                        (ref MessageWriter writer) =>
                        {
                            writer.WriteDictionary(properties);
                            writer.WriteStructureStart();
                            writer.WriteObjectPath(session);
                            writer.WriteArray(Array.Empty<byte>());
                            writer.WriteArray(bytes);
                            writer.WriteString(ContentType);
                            writer.WriteBool(true);
                        }),
                    static (Message reply, object? state) =>
                    {
                        var reader = reply.GetBodyReader();
                        reader.ReadObjectPathAsString();
                        return reader.ReadObjectPathAsString();
                    },
                    null));
                await AwaitPromptAsync(connection, prompt);
                return null;
            }
            finally
            {
                Array.Clear(bytes);
                await CloseSessionAsync(connection, session);
            }
        });
    }

    public void Clear(IReadOnlyDictionary<string, string> attributes)
    {
        Run<object?>(async connection =>
        {
            var (unlocked, locked) = await SearchAsync(connection, attributes);
            foreach (var item in unlocked)
            {
                await DeleteItemAsync(connection, item);
            }

            if (locked.Length > 0)
            {
                await UnlockAsync(connection, locked);
                foreach (var item in locked)
                {
                    await DeleteItemAsync(connection, item);
                }
            }

            return null;
        });
    }

    internal int CountItems(IReadOnlyDictionary<string, string> attributes)
    {
        return Run(async connection =>
        {
            var (unlocked, locked) = await SearchAsync(connection, attributes);
            return unlocked.Length + locked.Length;
        });
    }

    private async Task DeleteItemAsync(DBusConnection connection, string item)
    {
        var prompt = await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(connection, ServiceName, item, ItemInterface, "Delete"),
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                return reader.ReadObjectPathAsString();
            },
            null));
        await AwaitPromptAsync(connection, prompt);
    }

    private T Run<T>(Func<DBusConnection, Task<T>> operation)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new SecretServiceException("O barramento D-Bus da sessão não está disponível.");
        }

        DBusConnection? connection = null;
        try
        {
            connection = new DBusConnection(address);
            var opened = connection;
            var work = Task.Run(async () =>
            {
                await DBusBus.ConnectAsync(opened, timeout).ConfigureAwait(false);
                return await operation(opened).ConfigureAwait(false);
            });
            DBusBus.Observe(work);
            return work.GetAwaiter().GetResult();
        }
        catch (SecretServiceException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new SecretServiceException("O serviço de segredos não respondeu a tempo.", exception);
        }
        catch (DBusErrorReplyException exception)
        {
            throw new SecretServiceException($"O serviço de segredos recusou a operação ({exception.ErrorName}).", exception);
        }
        catch (Exception exception)
        {
            throw new SecretServiceException("Não foi possível falar com o serviço de segredos.", exception);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private async Task<T> Step<T>(Task<T> task)
    {
        DBusBus.Observe(task);
        return await task.WaitAsync(timeout).ConfigureAwait(false);
    }

    private async Task Step(Task task)
    {
        DBusBus.Observe(task);
        await task.WaitAsync(timeout).ConfigureAwait(false);
    }

    private async Task<string> OpenSessionAsync(DBusConnection connection)
    {
        return await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(
                connection,
                ServiceName,
                ServicePath,
                ServiceInterface,
                "OpenSession",
                "sv",
                (ref MessageWriter writer) =>
                {
                    writer.WriteString("plain");
                    writer.WriteVariantString(string.Empty);
                }),
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                reader.ReadVariantValue();
                return reader.ReadObjectPathAsString();
            },
            null));
    }

    private async Task CloseSessionAsync(DBusConnection connection, string session)
    {
        try
        {
            await Step(connection.CallMethodAsync(
                DBusBus.BuildCall(connection, ServiceName, session, SessionInterface, "Close")));
        }
        catch (Exception)
        {
        }
    }

    private async Task<(string[] Unlocked, string[] Locked)> SearchAsync(
        DBusConnection connection,
        IReadOnlyDictionary<string, string> attributes)
    {
        var found = await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(
                connection,
                ServiceName,
                ServicePath,
                ServiceInterface,
                "SearchItems",
                "a{ss}",
                (ref MessageWriter writer) => WriteAttributes(ref writer, attributes)),
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                var unlocked = reader.ReadArrayOfObjectPath().Select(static path => (string)path).ToArray();
                var locked = reader.ReadArrayOfObjectPath().Select(static path => (string)path).ToArray();
                return (unlocked, locked);
            },
            null));
        return (found.unlocked, found.locked);
    }

    private static void WriteAttributes(ref MessageWriter writer, IReadOnlyDictionary<string, string> attributes)
    {
        var start = writer.WriteDictionaryStart();
        foreach (var (key, value) in attributes)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(key);
            writer.WriteString(value);
        }

        writer.WriteDictionaryEnd(start);
    }

    private async Task<string?> ResolveCollectionAsync(DBusConnection connection)
    {
        var alias = await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(
                connection,
                ServiceName,
                ServicePath,
                ServiceInterface,
                "ReadAlias",
                "s",
                (ref MessageWriter writer) => writer.WriteString("default")),
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                return reader.ReadObjectPathAsString();
            },
            null));
        if (alias != NoObject)
        {
            return alias;
        }

        var collections = await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(
                connection,
                ServiceName,
                ServicePath,
                PropertiesInterface,
                "Get",
                "ss",
                (ref MessageWriter writer) =>
                {
                    writer.WriteString(ServiceInterface);
                    writer.WriteString("Collections");
                }),
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                reader.ReadSignature("ao");
                return reader.ReadArrayOfObjectPath().Select(static path => (string)path).ToArray();
            },
            null));
        if (collections.Contains(LoginCollectionPath))
        {
            return LoginCollectionPath;
        }

        return collections.FirstOrDefault(static path => path != SessionCollectionPath);
    }

    private async Task UnlockAsync(DBusConnection connection, string[] objects)
    {
        var prompt = await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(
                connection,
                ServiceName,
                ServicePath,
                ServiceInterface,
                "Unlock",
                "ao",
                (ref MessageWriter writer) => writer.WriteArray(objects.Select(static path => new ObjectPath(path)).ToArray())),
            static (Message reply, object? state) =>
            {
                var reader = reply.GetBodyReader();
                reader.ReadArrayOfObjectPath();
                return reader.ReadObjectPathAsString();
            },
            null));
        await AwaitPromptAsync(connection, prompt);
    }

    private async Task AwaitPromptAsync(DBusConnection connection, string prompt)
    {
        if (prompt == NoObject)
        {
            return;
        }

        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DBusBus.Observe(completed.Task);
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Path = prompt,
            Interface = PromptInterface,
            Member = "Completed",
        };
        MessageValueReader<bool> reader = static (message, state) =>
        {
            var body = message.GetBodyReader();
            return body.ReadBool();
        };
        using var subscription = await Step(connection.AddMatchAsync(
            rule,
            reader,
            notification =>
            {
                if (notification.HasValue)
                {
                    completed.TrySetResult(notification.Value);
                }
                else if (notification.Exception is { } failure)
                {
                    completed.TrySetException(failure);
                }
                else
                {
                    completed.TrySetException(new SecretServiceException("A conexão com o serviço de segredos foi encerrada."));
                }
            },
            false,
            ObserverFlags.EmitOnConnectionClosed | ObserverFlags.EmitOnConnectionFailed | ObserverFlags.EmitOnReaderFailed,
            null).AsTask());

        await Step(connection.CallMethodAsync(
            DBusBus.BuildCall(
                connection,
                ServiceName,
                prompt,
                PromptInterface,
                "Prompt",
                "s",
                (ref MessageWriter writer) => writer.WriteString(string.Empty))));

        bool dismissed;
        try
        {
            dismissed = await completed.Task.WaitAsync(promptTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            await DismissPromptAsync(connection, prompt);
            throw new SecretServiceException("O pedido de senha do cofre não foi respondido a tempo.", exception);
        }

        if (dismissed)
        {
            throw new SecretServiceException("O cofre de segredos está trancado ou o pedido de senha foi cancelado.");
        }
    }

    private static async Task DismissPromptAsync(DBusConnection connection, string prompt)
    {
        try
        {
            var call = connection.CallMethodAsync(
                DBusBus.BuildCall(connection, ServiceName, prompt, PromptInterface, "Dismiss"));
            DBusBus.Observe(call);
            await call.WaitAsync(DismissTimeout).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }
}
