namespace LGAC.Linux.Tests;

[TestClass]
public class ProgramArgumentsTests
{
    [TestMethod]
    public void NoArgumentsRunsTheApp()
    {
        Assert.AreEqual(LaunchMode.Run, ProgramArguments.Parse([]));
    }

    [TestMethod]
    public void VersionFlagIsRecognized()
    {
        Assert.AreEqual(LaunchMode.Version, ProgramArguments.Parse(["--version"]));
    }

    [TestMethod]
    public void SmokeFlagIsRecognized()
    {
        Assert.AreEqual(LaunchMode.Smoke, ProgramArguments.Parse(["--smoke"]));
    }

    [TestMethod]
    public void UnknownOrExtraArgumentsAreInvalid()
    {
        Assert.AreEqual(LaunchMode.Invalid, ProgramArguments.Parse(["--nope"]));
        Assert.AreEqual(LaunchMode.Invalid, ProgramArguments.Parse(["--version", "--smoke"]));
        Assert.AreEqual(LaunchMode.Invalid, ProgramArguments.Parse(["--VERSION"]));
        Assert.AreEqual(LaunchMode.Invalid, ProgramArguments.Parse([""]));
    }

    [TestMethod]
    public void VersionTextDropsTheSourceHash()
    {
        Assert.AreEqual("lg-ac 1.2.3", ProgramArguments.VersionText("1.2.3+abcdef0"));
        Assert.AreEqual("lg-ac 1.0.0", ProgramArguments.VersionText("1.0.0"));
        Assert.AreEqual("lg-ac 1.0.0-beta.1", ProgramArguments.VersionText("1.0.0-beta.1+1234"));
        Assert.AreEqual("lg-ac desconhecida", ProgramArguments.VersionText(null));
        Assert.AreEqual("lg-ac desconhecida", ProgramArguments.VersionText("  "));
    }
}
