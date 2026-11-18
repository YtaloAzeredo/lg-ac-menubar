using System.Text.Json;
using System.Text.Json.Nodes;

namespace LGAC.Core;

public interface ITokenVault
{
    string? Read();

    void Save(string token);

    void Delete();
}

public interface ICredentialStore
{
    string? Token { get; }

    void SaveToken(string token);

    void DeleteToken();

    string Country { get; set; }

    string? DeviceId { get; set; }

    string? DeviceAlias { get; set; }

    string ClientId { get; }
}

public sealed class FileCredentialStore(string settingsPath, ITokenVault vault) : ICredentialStore
{
    private const string CountryKey = "country";
    private const string DeviceIdKey = "deviceId";
    private const string DeviceAliasKey = "deviceAlias";
    private const string ClientIdKey = "clientId";
    private const int ReadAttempts = 3;
    private const int ReadRetryDelayMilliseconds = 20;

    private readonly object gate = new();
    private readonly string fullPath = Path.GetFullPath(settingsPath);
    private string? generatedClientId;

    public string? Token => vault.Read();

    public void SaveToken(string token)
    {
        vault.Save(token);
    }

    public void DeleteToken()
    {
        vault.Delete();
    }

    public string Country
    {
        get
        {
            lock (gate)
            {
                return Load().Settings.Get(CountryKey).AsString() ?? "BR";
            }
        }
        set => Update(CountryKey, value);
    }

    public string? DeviceId
    {
        get
        {
            lock (gate)
            {
                return Load().Settings.Get(DeviceIdKey).AsString();
            }
        }
        set => Update(DeviceIdKey, value);
    }

    public string? DeviceAlias
    {
        get
        {
            lock (gate)
            {
                return Load().Settings.Get(DeviceAliasKey).AsString();
            }
        }
        set => Update(DeviceAliasKey, value);
    }

    public string ClientId
    {
        get
        {
            lock (gate)
            {
                var (settings, readable) = Load();
                if (settings.Get(ClientIdKey).AsString() is { } existing)
                {
                    generatedClientId = existing;
                    return existing;
                }

                generatedClientId ??= $"lgac-{Guid.NewGuid().ToString().ToLowerInvariant()}";
                if (readable)
                {
                    settings[ClientIdKey] = generatedClientId;
                    Write(settings);
                }

                return generatedClientId;
            }
        }
    }

    private (JsonObject Settings, bool Readable) Load()
    {
        for (var attempt = 1; attempt <= ReadAttempts; attempt++)
        {
            try
            {
                var text = File.ReadAllText(fullPath);
                var settings = JsonNode.Parse(text) as JsonObject ?? [];
                settings.ContainsKey(CountryKey);
                return (settings, true);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException
                or JsonException or ArgumentException)
            {
                return ([], true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt < ReadAttempts)
                {
                    Thread.Sleep(ReadRetryDelayMilliseconds);
                }
            }
        }

        return ([], false);
    }

    private void Update(string key, string? value)
    {
        lock (gate)
        {
            var (settings, readable) = Load();
            if (!readable)
            {
                return;
            }

            if (value is null)
            {
                settings.Remove(key);
            }
            else
            {
                settings[key] = value;
            }

            Write(settings);
        }
    }

    private void Write(JsonObject settings)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, settings.ToJsonString());
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
