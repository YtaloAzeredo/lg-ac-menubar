using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACCapabilitiesTests
{
    [TestMethod]
    public void FullProfile()
    {
        var caps = new ACCapabilities(Fixture.Json("profile-full"));
        Assert.IsTrue(caps.Power);
        CollectionAssert.AreEqual(new[] { "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO" }, caps.JobModes.ToArray());
        Assert.AreEqual(new TemperatureControl("temperature", "targetTemperature", 18, 30, 1), caps.TargetTemp);
        CollectionAssert.AreEqual(new[] { "LOW", "MID", "HIGH", "AUTO" }, caps.WindStrengths.ToArray());
        Assert.AreEqual("windStrength", caps.WindStrengthKey);
        Assert.IsTrue(caps.SwingUpDown);
        Assert.IsTrue(caps.SwingLeftRight);
        Assert.AreEqual(OffTimerKind.Timer, caps.OffTimer);
        Assert.AreEqual(DisplayLightKind.OnOff, caps.DisplayLight);
    }

    [TestMethod]
    public void PrefersWindStrengthWhenBothAreWritable()
    {
        var profile = JsonNode.Parse("""
            {"property": {"airFlow": {
                "windStrength": {"mode": ["r", "w"], "type": "enum", "value": {"w": ["LOW", "MID", "HIGH", "AUTO"]}},
                "windStrengthDetail": {"mode": ["r", "w"], "type": "enum", "value": {"w": ["NATURE", "LOW", "MID", "HIGH"]}}
            }}}
            """);
        var caps = new ACCapabilities(profile);
        Assert.AreEqual("windStrength", caps.WindStrengthKey);
        CollectionAssert.AreEqual(new[] { "LOW", "MID", "HIGH", "AUTO" }, caps.WindStrengths.ToArray());
    }

    [TestMethod]
    public void BasicProfile()
    {
        var caps = new ACCapabilities(Fixture.Json("profile-basic"));
        Assert.IsTrue(caps.Power);
        CollectionAssert.AreEqual(new[] { "COOL", "FAN" }, caps.JobModes.ToArray());
        Assert.AreEqual(new TemperatureControl("temperatureInUnits", "targetTemperature", 16, 30, 0.5), caps.TargetTemp);
        Assert.AreEqual("windStrengthDetail", caps.WindStrengthKey);
        CollectionAssert.AreEqual(new[] { "LOW", "HIGH" }, caps.WindStrengths.ToArray());
        Assert.IsTrue(caps.SwingUpDown);
        Assert.IsFalse(caps.SwingLeftRight);
        Assert.IsNull(caps.OffTimer);
        Assert.IsNull(caps.DisplayLight);
    }

    [TestMethod]
    public void CoolTargetIsLastResort()
    {
        var profile = JsonNode.Parse("""
            {"property": {"temperatureInUnits": [{
                "unit": "C",
                "targetTemperature": {"type": "number", "mode": ["r"]},
                "coolTargetTemperature": {"type": "range", "mode": ["w"], "value": {"w": {"min": 18, "max": 30, "step": 1}}}
            }]}}
            """);
        Assert.AreEqual(
            new TemperatureControl("temperatureInUnits", "coolTargetTemperature", 18, 30, 1),
            new ACCapabilities(profile).TargetTemp);
    }

    [TestMethod]
    public void SleepTimerFallback()
    {
        var profile = JsonNode.Parse("""
            {"property": {"sleepTimer": {"relativeHourToStop": {"type": "number", "mode": ["r", "w"]}}}}
            """);
        Assert.AreEqual(OffTimerKind.SleepTimer, new ACCapabilities(profile).OffTimer);
    }

    [TestMethod]
    public void BooleanDisplayLight()
    {
        var profile = JsonNode.Parse("""
            {"property": {"display": {"light": {"type": "boolean", "mode": ["r", "w"], "value": {"w": [true, false]}}}}}
            """);
        Assert.AreEqual(DisplayLightKind.Boolean, new ACCapabilities(profile).DisplayLight);
    }

    [TestMethod]
    public void ReadOnlyControlsAreHidden()
    {
        var profile = JsonNode.Parse("""
            {"property": {"windDirection": {"rotateUpDown": {"type": "boolean", "mode": ["r"]}}}}
            """);
        Assert.IsFalse(new ACCapabilities(profile).SwingUpDown);
    }

    [TestMethod]
    public void EmptyProfile()
    {
        var caps = new ACCapabilities(JsonNode.Parse("{}"));
        Assert.IsFalse(caps.Power);
        Assert.IsEmpty(caps.JobModes);
        Assert.IsNull(caps.TargetTemp);
        Assert.IsEmpty(caps.WindStrengths);
        Assert.IsNull(caps.OffTimer);
        Assert.IsNull(caps.DisplayLight);
    }

    [TestMethod]
    public void ClampRoundsToStepAndRange()
    {
        var temp = new TemperatureControl("temperature", "targetTemperature", 16, 30, 0.5);
        Assert.AreEqual(30, temp.Clamp(31));
        Assert.AreEqual(16, temp.Clamp(15));
        Assert.AreEqual(23.5, temp.Clamp(23.3));
        Assert.AreEqual(23, temp.Clamp(23.2));
    }

    [TestMethod]
    public void EqualProfilesAreEqual()
    {
        var first = new ACCapabilities(Fixture.Json("profile-full"));
        var second = new ACCapabilities(Fixture.Json("profile-full"));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreNotEqual(first, new ACCapabilities(Fixture.Json("profile-basic")));
    }
}
