using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class FileCredentialStoreTests
{
    private string directory = "";

    private string SettingsPath => Path.Combine(directory, "settings.json");

    [TestInitialize]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "lgac-tests-" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private FileCredentialStore NewStore(InMemoryTokenVault? vault = null)
    {
        return new FileCredentialStore(SettingsPath, vault ?? new InMemoryTokenVault());
    }

    private void AssertDefaults(FileCredentialStore store)
    {
        Assert.AreEqual("BR", store.Country);
        Assert.IsNull(store.DeviceId);
        Assert.IsNull(store.DeviceAlias);
        Assert.IsTrue(store.ClientId.StartsWith("lgac-", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DefaultsWhenFileIsMissing()
    {
        AssertDefaults(NewStore());
    }

    [TestMethod]
    public void DefaultsWhenFileIsEmpty()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, "");
        AssertDefaults(NewStore());
    }

    [TestMethod]
    public void DefaultsWhenFileIsCorrupt()
    {
        Directory.CreateDirectory(directory);
        foreach (var content in new[] { "not json {", "[1, 2]", "42", "{\"country\": 5, \"deviceId\": [], \"deviceAlias\": {}, \"clientId\": true}" })
        {
            File.WriteAllText(SettingsPath, content);
            AssertDefaults(NewStore());
        }
    }

    [TestMethod]
    public void CorruptFileIsReplacedOnNextWrite()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, "not json {");
        var store = NewStore();
        store.Country = "US";
        Assert.AreEqual("US", NewStore().Country);
    }

    [TestMethod]
    public void CreatesDirectoryOnFirstWrite()
    {
        Assert.IsFalse(Directory.Exists(directory));
        NewStore().DeviceId = "ac-1";
        Assert.IsTrue(File.Exists(SettingsPath));
    }

    [TestMethod]
    public void PreferencesRoundTripAcrossInstances()
    {
        var first = NewStore();
        first.Country = "US";
        first.DeviceId = "ac-1";
        first.DeviceAlias = "Sala";

        var second = NewStore();
        Assert.AreEqual("US", second.Country);
        Assert.AreEqual("ac-1", second.DeviceId);
        Assert.AreEqual("Sala", second.DeviceAlias);
    }

    [TestMethod]
    public void SettingNullClearsDevice()
    {
        var store = NewStore();
        store.DeviceId = "ac-1";
        store.DeviceAlias = "Sala";
        store.DeviceId = null;
        store.DeviceAlias = null;

        var other = NewStore();
        Assert.IsNull(other.DeviceId);
        Assert.IsNull(other.DeviceAlias);
    }

    [TestMethod]
    public void ClientIdIsStableAcrossInstances()
    {
        var first = NewStore().ClientId;
        Assert.AreEqual(first, NewStore().ClientId);
    }

    [TestMethod]
    public void WritingPreferencesKeepsClientId()
    {
        var store = NewStore();
        var clientId = store.ClientId;
        store.Country = "US";
        store.DeviceId = "ac-1";
        Assert.AreEqual(clientId, store.ClientId);
        Assert.AreEqual(clientId, NewStore().ClientId);
    }

    [TestMethod]
    public void WritingClientIdKeepsPreferences()
    {
        var store = NewStore();
        store.Country = "US";
        store.DeviceAlias = "Sala";
        _ = NewStore().ClientId;
        Assert.AreEqual("US", store.Country);
        Assert.AreEqual("Sala", NewStore().DeviceAlias);
    }

    [TestMethod]
    public void ClientIdHasPrefixAndIsLowercase()
    {
        var clientId = NewStore().ClientId;
        Assert.IsTrue(clientId.StartsWith("lgac-", StringComparison.Ordinal));
        Assert.AreEqual(clientId.ToLowerInvariant(), clientId);
        Assert.IsTrue(Guid.TryParse(clientId["lgac-".Length..], out _));
    }

    [TestMethod]
    public void TokenGoesToVaultAndNeverToFile()
    {
        var vault = new InMemoryTokenVault();
        var store = NewStore(vault);
        store.Country = "US";
        store.SaveToken("segredo-ficticio-123");

        Assert.AreEqual("segredo-ficticio-123", store.Token);
        Assert.AreEqual("segredo-ficticio-123", vault.Read());
        Assert.IsFalse(File.ReadAllText(SettingsPath).Contains("segredo-ficticio-123", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DeleteTokenClearsVault()
    {
        var vault = new InMemoryTokenVault();
        var store = NewStore(vault);
        store.SaveToken("primeiro");
        store.SaveToken("segundo");
        Assert.AreEqual("segundo", store.Token);

        store.DeleteToken();
        Assert.IsNull(store.Token);
        Assert.IsNull(vault.Read());
    }

    [TestMethod]
    public void UnreadableFileIsNotOverwritten()
    {
        Directory.CreateDirectory(SettingsPath);
        var store = NewStore();

        Assert.AreEqual("BR", store.Country);
        Assert.IsNull(store.DeviceId);
        Assert.IsNull(store.DeviceAlias);

        store.Country = "US";
        store.DeviceId = "ac-1";
        store.DeviceAlias = "Sala";
        var clientId = store.ClientId;

        Assert.AreEqual(clientId, store.ClientId);
        Assert.IsTrue(clientId.StartsWith("lgac-", StringComparison.Ordinal));
        Assert.AreEqual("BR", store.Country);
        Assert.IsTrue(Directory.Exists(SettingsPath));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(SettingsPath).Length);
        Assert.AreEqual(1, Directory.GetFileSystemEntries(directory).Length);
    }

    [TestMethod]
    public void ConcurrentWritesKeepAllKeys()
    {
        var store = NewStore();
        var clientIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 200, index =>
        {
            switch (index % 4)
            {
                case 0:
                    store.Country = "US";
                    break;
                case 1:
                    store.DeviceId = "ac-1";
                    break;
                case 2:
                    store.DeviceAlias = "Sala";
                    break;
                default:
                    clientIds.Add(store.ClientId);
                    break;
            }
        });

        var other = NewStore();
        Assert.AreEqual("US", other.Country);
        Assert.AreEqual("ac-1", other.DeviceId);
        Assert.AreEqual("Sala", other.DeviceAlias);
        Assert.AreEqual(1, clientIds.Distinct().Count());
        Assert.AreEqual(clientIds.First(), other.ClientId);
    }

    [TestMethod]
    public void DuplicateKeysCountAsCorrupt()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, "{\"country\": \"US\", \"country\": \"CA\"}");
        var store = NewStore();
        Assert.AreEqual("BR", store.Country);
        store.Country = "AR";
        Assert.AreEqual("AR", NewStore().Country);
    }

    [TestMethod]
    public void LeavesNoTemporaryFiles()
    {
        var store = NewStore();
        store.Country = "US";
        store.DeviceId = "ac-1";
        store.DeviceId = null;
        _ = store.ClientId;

        CollectionAssert.AreEqual(new[] { SettingsPath }, Directory.GetFileSystemEntries(directory));
    }
}
