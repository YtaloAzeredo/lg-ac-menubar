using LGAC.App.Platform;

namespace LGAC.App.Tests;

[TestClass]
public sealed class StartupRegistrationTests
{
    private const string Executable = @"C:\Apps\LG AC\LG AC.exe";

    [TestMethod]
    public void EnableWritesQuotedPath()
    {
        var key = new MemoryRunKey();
        var registration = new StartupRegistration(key, Executable);

        registration.IsEnabled = true;

        Assert.IsTrue(registration.IsEnabled);
        Assert.AreEqual("\"" + Executable + "\"", key.Values["LG AC"]);
    }

    [TestMethod]
    public void DisableRemovesValue()
    {
        var key = new MemoryRunKey();
        var registration = new StartupRegistration(key, Executable);
        registration.IsEnabled = true;

        registration.IsEnabled = false;

        Assert.IsFalse(registration.IsEnabled);
        Assert.IsFalse(key.Values.ContainsKey("LG AC"));
    }

    [TestMethod]
    public void RepairUpdatesStalePath()
    {
        var key = new MemoryRunKey();
        key.Values["LG AC"] = "\"D:\\Old\\LG AC.exe\"";
        var registration = new StartupRegistration(key, Executable);

        registration.RepairPath();

        Assert.AreEqual("\"" + Executable + "\"", key.Values["LG AC"]);
    }

    [TestMethod]
    public void RepairDoesNothingWhenDisabled()
    {
        var key = new MemoryRunKey();
        var registration = new StartupRegistration(key, Executable);

        registration.RepairPath();

        Assert.IsFalse(registration.IsEnabled);
        Assert.IsEmpty(key.Values);
    }

    [TestMethod]
    public void RepairKeepsCurrentPath()
    {
        var key = new MemoryRunKey();
        key.Values["LG AC"] = "\"" + Executable.ToLowerInvariant() + "\"";
        var registration = new StartupRegistration(key, Executable);

        registration.RepairPath();

        Assert.AreEqual(0, key.Writes);
    }

    private sealed class MemoryRunKey : IRunKey
    {
        public int Writes { get; private set; }

        public Dictionary<string, string> Values { get; } = [];

        public string? Get(string name)
        {
            return Values.TryGetValue(name, out var value) ? value : null;
        }

        public void Set(string name, string value)
        {
            Writes++;
            Values[name] = value;
        }

        public void Remove(string name)
        {
            Values.Remove(name);
        }
    }
}
