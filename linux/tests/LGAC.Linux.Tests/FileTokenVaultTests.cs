using System.Runtime.Versioning;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class FileTokenVaultTests
{
    private string root = "";

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), $"lgac-vault-{Guid.NewGuid():N}");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(root, true);
        }
        catch (IOException)
        {
        }
    }

    private string TokenPath => Path.Combine(root, "config", "token");

    [TestMethod]
    public void MissingFileReadsNull()
    {
        Assert.IsNull(new FileTokenVault(TokenPath).Read());
    }

    [TestMethod]
    public void RoundTripsAndOverwrites()
    {
        var vault = new FileTokenVault(TokenPath);

        vault.Save("fake-token-one");
        Assert.AreEqual("fake-token-one", vault.Read());

        vault.Save("fake-token-two");
        Assert.AreEqual("fake-token-two", vault.Read());
    }

    [TestMethod]
    public void ReadTrimsAndTreatsBlankAsNull()
    {
        var vault = new FileTokenVault(TokenPath);
        Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)!);

        File.WriteAllText(TokenPath, "  fake-token\n");
        Assert.AreEqual("fake-token", vault.Read());

        File.WriteAllText(TokenPath, " \n");
        Assert.IsNull(vault.Read());
    }

    [TestMethod]
    public void DeleteIsIdempotent()
    {
        var vault = new FileTokenVault(TokenPath);
        vault.Save("fake-token");

        vault.Delete();
        vault.Delete();

        Assert.IsNull(vault.Read());
        Assert.IsFalse(File.Exists(TokenPath));
    }

    [TestMethod]
    public void SaveLeavesNoTemporaryFiles()
    {
        var vault = new FileTokenVault(TokenPath);

        vault.Save("fake-token");

        CollectionAssert.AreEqual(new[] { TokenPath }, Directory.GetFiles(Path.GetDirectoryName(TokenPath)!));
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void FileIsOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive();
        }

        var vault = new FileTokenVault(TokenPath);
        vault.Save("fake-token");
        vault.Save("fake-token-again");

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(TokenPath));
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(Path.GetDirectoryName(TokenPath)!));
    }

    [TestMethod]
    public void SaveThrowsOnIoFailureWithoutLeakingTheToken()
    {
        Directory.CreateDirectory(root);
        var blocker = Path.Combine(root, "blocker");
        File.WriteAllText(blocker, "x");
        var vault = new FileTokenVault(Path.Combine(blocker, "token"));

        var exception = Assert.ThrowsExactly<IOException>(() => vault.Save("fake-secret-token"));

        Assert.IsFalse(exception.ToString().Contains("fake-secret-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReadAndDeleteNeverThrow()
    {
        Directory.CreateDirectory(root);
        var blocker = Path.Combine(root, "blocker");
        File.WriteAllText(blocker, "x");
        var vault = new FileTokenVault(Path.Combine(blocker, "token"));

        Assert.IsNull(vault.Read());
        vault.Delete();
    }

    [TestMethod]
    public void ReadOfADirectoryReturnsNull()
    {
        Directory.CreateDirectory(TokenPath);

        Assert.IsNull(new FileTokenVault(TokenPath).Read());
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void ExistingDirectoryIsTightened()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive();
        }

        var directory = Path.GetDirectoryName(TokenPath)!;
        Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory, (UnixFileMode)0b111_101_101);

        new FileTokenVault(TokenPath).Save("fake-token");

        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(directory));
    }
}
