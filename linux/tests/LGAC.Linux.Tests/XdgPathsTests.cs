using System.Runtime.Versioning;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class XdgPathsTests
{
    private string home = "";
    private string runtime = "";

    [TestInitialize]
    public void Setup()
    {
        home = Path.Combine(Path.GetTempPath(), $"lgac-xdg-{Guid.NewGuid():N}", "home");
        runtime = Path.Combine(Path.GetTempPath(), $"lgac-xdg-{Guid.NewGuid():N}", "run");
        Directory.CreateDirectory(runtime);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TryDelete(Path.GetDirectoryName(home)!);
        TryDelete(Path.GetDirectoryName(runtime)!);
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
    }

    private static Func<string, string?> Env(Dictionary<string, string> values) =>
        name => values.GetValueOrDefault(name);

    [TestMethod]
    public void UsesDefaultsUnderHome()
    {
        var paths = new XdgPaths(Env([]), home);

        Assert.AreEqual(Path.Combine(home, ".config"), paths.ConfigHome);
        Assert.AreEqual(Path.Combine(home, ".local", "share"), paths.DataHome);
        Assert.AreEqual(Path.Combine(home, ".config", "lg-ac"), paths.AppConfigDir);
        Assert.AreEqual(Path.Combine(home, ".config", "lg-ac", "settings.json"), paths.SettingsFile);
        Assert.AreEqual(Path.Combine(home, ".config", "lg-ac", "token"), paths.TokenFile);
        Assert.AreEqual(Path.Combine(home, ".config", "autostart", "lg-ac.desktop"), paths.AutostartFile);
    }

    [TestMethod]
    public void HonorsAbsoluteVariables()
    {
        var config = Path.Combine(home, "cfg");
        var data = Path.Combine(home, "dat");
        var paths = new XdgPaths(
            Env(new() { ["XDG_CONFIG_HOME"] = config, ["XDG_DATA_HOME"] = data, ["XDG_RUNTIME_DIR"] = runtime }),
            home);

        Assert.AreEqual(config, paths.ConfigHome);
        Assert.AreEqual(data, paths.DataHome);
        Assert.AreEqual(Path.Combine(config, "autostart", "lg-ac.desktop"), paths.AutostartFile);
        Assert.AreEqual(runtime, paths.RuntimeDir);
        Assert.AreEqual(Path.Combine(runtime, "lg-ac.lock"), paths.LockFile);
        Assert.AreEqual(Path.Combine(runtime, "lg-ac.sock"), paths.SocketFile);
    }

    [TestMethod]
    public void IgnoresEmptyAndRelativeValues()
    {
        var paths = new XdgPaths(
            Env(new() { ["XDG_CONFIG_HOME"] = "", ["XDG_DATA_HOME"] = "relative/data", ["XDG_RUNTIME_DIR"] = "run" }),
            home);

        Assert.AreEqual(Path.Combine(home, ".config"), paths.ConfigHome);
        Assert.AreEqual(Path.Combine(home, ".local", "share"), paths.DataHome);
        Assert.IsTrue(Path.IsPathRooted(paths.RuntimeDir));
        Assert.IsTrue(paths.RuntimeDir.StartsWith(Path.GetTempPath(), StringComparison.Ordinal));
    }

    [TestMethod]
    public void RuntimeDirFallsBackWhenMissingOnDisk()
    {
        var missing = Path.Combine(runtime, "nope");
        var paths = new XdgPaths(Env(new() { ["XDG_RUNTIME_DIR"] = missing }), home);

        Assert.AreNotEqual(missing, paths.RuntimeDir);
        Assert.IsTrue(paths.RuntimeDir.StartsWith(Path.GetTempPath(), StringComparison.Ordinal));
        Assert.IsTrue(paths.RuntimeDir.Contains("lg-ac-", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EmptyHomeNeverYieldsRelativePaths()
    {
        var paths = new XdgPaths(Env([]), "");

        Assert.IsTrue(Path.IsPathRooted(paths.ConfigHome));
        Assert.IsTrue(Path.IsPathRooted(paths.DataHome));
        Assert.IsTrue(Path.IsPathRooted(paths.TokenFile));
    }

    [TestMethod]
    public void FromEnvironmentProducesRootedPaths()
    {
        var paths = XdgPaths.FromEnvironment();

        Assert.IsTrue(Path.IsPathRooted(paths.SettingsFile));
        Assert.IsTrue(Path.IsPathRooted(paths.SocketFile));
    }

    [TestMethod]
    public void RelativeTempBaseStillYieldsRootedPaths()
    {
        var paths = new XdgPaths(Env([]), "", "relative-tmp");

        Assert.IsTrue(Path.IsPathRooted(paths.RuntimeDir));
        Assert.IsTrue(Path.IsPathRooted(paths.ConfigHome));
        Assert.IsTrue(Path.IsPathRooted(paths.SocketFile));
    }

    [TestMethod]
    public void FallbackNameDoesNotUseTheSanitizedUserName()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive();
        }

        var paths = new XdgPaths(Env([]), home, Path.GetDirectoryName(home)!);

        var name = Path.GetFileName(paths.RuntimeDir);
        StringAssert.StartsWith(name, "lg-ac-");
        Assert.IsTrue(name["lg-ac-".Length..].All(char.IsAsciiDigit));
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void EnsureCreatesFallbackWithOwnerOnlyMode()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive();
        }

        var tempBase = Path.GetDirectoryName(home)!;
        var paths = new XdgPaths(Env([]), home, tempBase);

        Assert.IsTrue(paths.EnsurePrivateRuntimeDir());
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(paths.RuntimeDir));
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void EnsureTightensExistingOpenDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive();
        }

        var tempBase = Path.GetDirectoryName(home)!;
        var paths = new XdgPaths(Env([]), home, tempBase);
        Directory.CreateDirectory(paths.RuntimeDir);
        File.SetUnixFileMode(paths.RuntimeDir, (UnixFileMode)0b111_101_101);

        Assert.IsTrue(paths.EnsurePrivateRuntimeDir());
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(paths.RuntimeDir));
    }

    [TestMethod]
    public void EnsureReturnsFalseWhenPathIsAFile()
    {
        var tempBase = Path.GetDirectoryName(home)!;
        var paths = new XdgPaths(Env([]), home, tempBase);
        Directory.CreateDirectory(tempBase);
        File.WriteAllText(paths.RuntimeDir, "x");

        Assert.IsFalse(paths.EnsurePrivateRuntimeDir());
    }

    [TestMethod]
    public void EnsureOnlyConfirmsConfiguredRuntimeDir()
    {
        var paths = new XdgPaths(Env(new() { ["XDG_RUNTIME_DIR"] = runtime }), home);

        Assert.IsTrue(paths.EnsurePrivateRuntimeDir());
    }
}
