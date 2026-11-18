using System.Text;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class AutostartEntryTests
{
    private string root = "";

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), $"lgac-autostart-{Guid.NewGuid():N}");
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

    private string DesktopFile => Path.Combine(root, "autostart", "lg-ac.desktop");

    [TestMethod]
    public void EnablingWritesAndReadsBack()
    {
        var entry = new AutostartEntry(DesktopFile, "/opt/lg-ac/lg-ac");

        entry.IsEnabled = true;

        Assert.IsTrue(entry.IsEnabled);
        Assert.IsTrue(new AutostartEntry(DesktopFile, "/opt/lg-ac/lg-ac").IsEnabled);
    }

    [TestMethod]
    public void WritesExactContentWithoutBom()
    {
        new AutostartEntry(DesktopFile, "/opt/lg-ac/lg-ac").IsEnabled = true;

        var bytes = File.ReadAllBytes(DesktopFile);
        Assert.AreNotEqual((byte)0xEF, bytes[0]);
        Assert.AreEqual(
            "[Desktop Entry]\nType=Application\nName=LG AC\nExec=\"/opt/lg-ac/lg-ac\"\nIcon=lg-ac\nTerminal=false\nX-GNOME-Autostart-enabled=true\n",
            Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public void DisablingDeletesTheFile()
    {
        var entry = new AutostartEntry(DesktopFile, "/opt/lg-ac/lg-ac") { IsEnabled = true };

        entry.IsEnabled = false;
        entry.IsEnabled = false;

        Assert.IsFalse(File.Exists(DesktopFile));
        Assert.IsFalse(entry.IsEnabled);
    }

    [TestMethod]
    public void CreatesMissingDirectory()
    {
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(DesktopFile)));

        new AutostartEntry(DesktopFile, "/opt/lg-ac/lg-ac").IsEnabled = true;

        Assert.IsTrue(File.Exists(DesktopFile));
    }

    [TestMethod]
    [DataRow("/opt/my apps/lg ac/lg-ac")]
    [DataRow("/opt/$HOME/lg-ac")]
    [DataRow("/opt/we\"ird/lg-ac")]
    [DataRow("/opt/back\\slash/lg-ac")]
    [DataRow("/opt/100%/lg-ac")]
    [DataRow("/opt/`tick`/lg-ac")]
    [DataRow("/opt/ação ü/lg-ac")]
    [DataRow("/opt/all $\"`\\% mixed/lg-ac")]
    public void ExecEscapingRoundTrips(string path)
    {
        var entry = new AutostartEntry(DesktopFile, path) { IsEnabled = true };

        Assert.AreEqual(path, AutostartEntry.DecodeExec(File.ReadAllLines(DesktopFile).Single(l => l.StartsWith("Exec=", StringComparison.Ordinal))["Exec=".Length..]));
        Assert.IsTrue(entry.IsEnabled);
    }

    [TestMethod]
    public void EscapesPerDesktopEntrySpecification()
    {
        new AutostartEntry(DesktopFile, "/a b/$x\"y`z\\w%v") { IsEnabled = true };

        var exec = File.ReadAllLines(DesktopFile).Single(l => l.StartsWith("Exec=", StringComparison.Ordinal));
        Assert.AreEqual("Exec=\"/a b/\\\\$x\\\\\"y\\\\`z\\\\\\\\w%%v\"", exec);
    }

    [TestMethod]
    public void RepairUpdatesOldPath()
    {
        new AutostartEntry(DesktopFile, "/old/lg-ac").IsEnabled = true;
        var entry = new AutostartEntry(DesktopFile, "/new place/lg-ac");

        entry.RepairPath();

        StringAssert.Contains(File.ReadAllText(DesktopFile), "Exec=\"/new place/lg-ac\"");
    }

    [TestMethod]
    public void RepairKeepsDisabledStateWithoutCreatingTheFile()
    {
        new AutostartEntry(DesktopFile, "/new/lg-ac").RepairPath();

        Assert.IsFalse(File.Exists(DesktopFile));
    }

    [TestMethod]
    public void RepairDoesNotRewriteWhenEqual()
    {
        var entry = new AutostartEntry(DesktopFile, "/opt/lg-ac") { IsEnabled = true };
        var marker = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(DesktopFile, marker);

        entry.RepairPath();

        Assert.AreEqual(marker, File.GetLastWriteTimeUtc(DesktopFile));
    }

    [TestMethod]
    public void RepairIsCaseSensitive()
    {
        new AutostartEntry(DesktopFile, "/opt/LG-AC").IsEnabled = true;

        new AutostartEntry(DesktopFile, "/opt/lg-ac").RepairPath();

        StringAssert.Contains(File.ReadAllText(DesktopFile), "Exec=\"/opt/lg-ac\"");
    }

    [TestMethod]
    public void HiddenTrueCountsAsDisabled()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
        File.WriteAllText(DesktopFile, "[Desktop Entry]\nType=Application\nExec=\"/opt/lg-ac\"\nHidden=true\n");

        Assert.IsFalse(new AutostartEntry(DesktopFile, "/opt/lg-ac").IsEnabled);
    }

    [TestMethod]
    public void FileWithoutExecIsDisabled()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
        File.WriteAllText(DesktopFile, "[Desktop Entry]\nType=Application\n");

        Assert.IsFalse(new AutostartEntry(DesktopFile, "/opt/lg-ac").IsEnabled);
    }

    [TestMethod]
    public void IoFailuresDoNotThrow()
    {
        Directory.CreateDirectory(root);
        var blocker = Path.Combine(root, "blocker");
        File.WriteAllText(blocker, "x");
        var entry = new AutostartEntry(Path.Combine(blocker, "lg-ac.desktop"), "/opt/lg-ac");

        entry.IsEnabled = true;
        entry.RepairPath();
        entry.IsEnabled = false;

        Assert.IsFalse(entry.IsEnabled);
    }

    [TestMethod]
    public void DirectoryInPlaceOfFileDoesNotThrow()
    {
        Directory.CreateDirectory(DesktopFile);
        var entry = new AutostartEntry(DesktopFile, "/opt/lg-ac");

        Assert.IsFalse(entry.IsEnabled);
        entry.RepairPath();
        entry.IsEnabled = false;
    }

    [TestMethod]
    public void GnomeDisabledCountsAsDisabled()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
        File.WriteAllText(DesktopFile, "[Desktop Entry]\nType=Application\nExec=\"/opt/lg-ac\"\nX-GNOME-Autostart-enabled=false\n");

        Assert.IsFalse(new AutostartEntry(DesktopFile, "/opt/lg-ac").IsEnabled);
    }

    [TestMethod]
    public void RepairDoesNotReenableGnomeDisabled()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
        const string original = "[Desktop Entry]\nType=Application\nExec=\"/old/lg-ac\"\nX-GNOME-Autostart-enabled=false\n";
        File.WriteAllText(DesktopFile, original);

        new AutostartEntry(DesktopFile, "/new/lg-ac").RepairPath();

        Assert.AreEqual(original, File.ReadAllText(DesktopFile));
    }

    [TestMethod]
    public void OnlyTheDesktopEntryGroupIsConsidered()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);
        File.WriteAllText(
            DesktopFile,
            "[Desktop Entry]\nType=Application\nExec=\"/opt/lg-ac\"\n\n[Desktop Action other]\nExec=/bin/other\nHidden=true\n");
        var entry = new AutostartEntry(DesktopFile, "/opt/lg-ac");

        Assert.IsTrue(entry.IsEnabled);
        entry.RepairPath();
        StringAssert.Contains(File.ReadAllText(DesktopFile), "[Desktop Action other]");
        Assert.IsFalse(File.ReadAllText(DesktopFile).Contains("Exec=\"/opt/lg-ac\"\nIcon", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ControlCharactersRoundTripOnOneLine()
    {
        const string path = "/opt/a\nb\tc\rd/lg-ac";
        var entry = new AutostartEntry(DesktopFile, path) { IsEnabled = true };

        var lines = File.ReadAllLines(DesktopFile);
        Assert.AreEqual(1, lines.Count(l => l.StartsWith("Exec=", StringComparison.Ordinal)));
        Assert.HasCount(7, lines);
        Assert.IsTrue(entry.IsEnabled);
        Assert.AreEqual(path, AutostartEntry.DecodeExec(lines.Single(l => l.StartsWith("Exec=", StringComparison.Ordinal))["Exec=".Length..]));
    }

    [TestMethod]
    public void ControlCharactersAreEncodedLiterally()
    {
        Assert.AreEqual("\"/a\\nb\\tc\"", AutostartEntry.EncodeExec("/a\nb\tc"));
        Assert.AreEqual("/a b", AutostartEntry.DecodeExec("\"/a\\sb\""));
    }

    [TestMethod]
    public void InvalidPathDoesNotThrow()
    {
        var entry = new AutostartEntry("", "/opt/lg-ac");

        Assert.IsFalse(entry.IsEnabled);
        entry.IsEnabled = true;
        entry.RepairPath();
        entry.IsEnabled = false;
        Assert.IsFalse(entry.IsEnabled);
    }
}
