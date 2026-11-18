using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACCommandTests
{
    private readonly ACCapabilities full = new(Fixture.Json("profile-full"));
    private readonly ACCapabilities basic = new(Fixture.Json("profile-basic"));

    private static void AssertPayload(string? expected, JsonObject? actual)
    {
        if (expected is null)
        {
            Assert.IsNull(actual);
            return;
        }

        Assert.IsNotNull(actual);
        Assert.IsTrue(
            JsonNode.DeepEquals(JsonNode.Parse(expected), actual),
            $"esperado {expected}, obtido {actual.ToJsonString()}");
    }

    [TestMethod]
    public void Power()
    {
        AssertPayload("""{"operation": {"airConOperationMode": "POWER_ON"}}""", new ACCommand.Power(true).Payload(full));
        AssertPayload("""{"operation": {"airConOperationMode": "POWER_OFF"}}""", new ACCommand.Power(false).Payload(full));
    }

    [TestMethod]
    public void JobMode()
    {
        AssertPayload("""{"airConJobMode": {"currentJobMode": "HEAT"}}""", new ACCommand.JobMode("HEAT").Payload(full));
        AssertPayload(null, new ACCommand.JobMode("HEAT").Payload(basic));
    }

    [TestMethod]
    public void TargetTempOnLegacyResource()
    {
        AssertPayload("""{"temperature": {"targetTemperature": 24}}""", new ACCommand.TargetTemp(24).Payload(full));
    }

    [TestMethod]
    public void TargetTempInUnitsIsClampedAndCarriesUnit()
    {
        AssertPayload(
            """{"temperatureInUnits": {"targetTemperature": 22.5, "unit": "C"}}""",
            new ACCommand.TargetTemp(22.5).Payload(basic));
        AssertPayload(
            """{"temperatureInUnits": {"targetTemperature": 30, "unit": "C"}}""",
            new ACCommand.TargetTemp(40).Payload(basic));
    }

    [TestMethod]
    public void IntegerTemperatureSerializesWithoutFraction()
    {
        Assert.AreEqual(
            """{"temperature":{"targetTemperature":24}}""",
            new ACCommand.TargetTemp(24).Payload(full)!.ToJsonString());
        Assert.AreEqual(
            """{"temperatureInUnits":{"targetTemperature":22.5,"unit":"C"}}""",
            new ACCommand.TargetTemp(22.5).Payload(basic)!.ToJsonString());
        Assert.AreEqual(
            """{"timer":{"relativeHourToStop":1,"relativeMinuteToStop":30}}""",
            new ACCommand.OffTimer(90).Payload(full)!.ToJsonString());
    }

    [TestMethod]
    public void WindStrengthUsesProfileKey()
    {
        AssertPayload("""{"airFlow": {"windStrength": "HIGH"}}""", new ACCommand.WindStrength("HIGH").Payload(full));
        AssertPayload("""{"airFlow": {"windStrengthDetail": "HIGH"}}""", new ACCommand.WindStrength("HIGH").Payload(basic));
        AssertPayload(null, new ACCommand.WindStrength("MID").Payload(basic));
    }

    [TestMethod]
    public void Swing()
    {
        AssertPayload("""{"windDirection": {"rotateUpDown": true}}""", new ACCommand.SwingUpDown(true).Payload(full));
        AssertPayload("""{"windDirection": {"rotateLeftRight": false}}""", new ACCommand.SwingLeftRight(false).Payload(full));
        AssertPayload(null, new ACCommand.SwingLeftRight(true).Payload(basic));
    }

    [TestMethod]
    public void OffTimer()
    {
        AssertPayload("""{"timer": {"relativeHourToStop": 2}}""", new ACCommand.OffTimer(120).Payload(full));
        AssertPayload(
            """{"timer": {"relativeHourToStop": 1, "relativeMinuteToStop": 30}}""",
            new ACCommand.OffTimer(90).Payload(full));
        AssertPayload(
            """{"timer": {"relativeHourToStop": 0, "relativeMinuteToStop": 30}}""",
            new ACCommand.OffTimer(30).Payload(full));
        AssertPayload("""{"timer": {"relativeStopTimer": "UNSET"}}""", new ACCommand.OffTimer(null).Payload(full));
        AssertPayload(null, new ACCommand.OffTimer(60).Payload(basic));
    }

    [TestMethod]
    public void OffTimerOnSleepTimerModels()
    {
        var caps = new ACCapabilities(JsonNode.Parse("""
            {"property": {"sleepTimer": {"relativeHourToStop": {"type": "number", "mode": ["r", "w"]}}}}
            """));
        AssertPayload("""{"sleepTimer": {"relativeHourToStop": 1}}""", new ACCommand.OffTimer(60).Payload(caps));
    }

    [TestMethod]
    public void DisplayLight()
    {
        AssertPayload("""{"display": {"light": "OFF"}}""", new ACCommand.DisplayLight(false).Payload(full));
        AssertPayload(null, new ACCommand.DisplayLight(true).Payload(basic));
        var booleanCaps = new ACCapabilities(JsonNode.Parse("""
            {"property": {"display": {"light": {"type": "boolean", "mode": ["r", "w"]}}}}
            """));
        AssertPayload("""{"display": {"light": true}}""", new ACCommand.DisplayLight(true).Payload(booleanCaps));
    }

    [TestMethod]
    public void ApplyUpdatesOnlyTheTouchedField()
    {
        var before = new ACState { IsOn = true, JobMode = "COOL", TargetTemp = 23, OffTimerRemainingMinutes = 45 };
        Assert.IsFalse(new ACCommand.Power(false).Apply(before).IsOn);
        Assert.AreEqual(before with { JobMode = "FAN" }, new ACCommand.JobMode("FAN").Apply(before));
        Assert.AreEqual(25, new ACCommand.TargetTemp(25).Apply(before).TargetTemp);
        Assert.IsNull(new ACCommand.OffTimer(null).Apply(before).OffTimerRemainingMinutes);
        Assert.IsNull(new ACCommand.OffTimer(0).Apply(before).OffTimerRemainingMinutes);
        Assert.AreEqual(120, new ACCommand.OffTimer(120).Apply(before).OffTimerRemainingMinutes);
        Assert.IsFalse(new ACCommand.DisplayLight(false).Apply(before).DisplayLightOn);
        Assert.AreEqual("HIGH", new ACCommand.WindStrength("HIGH").Apply(before).WindStrength);
        Assert.IsTrue(new ACCommand.SwingUpDown(true).Apply(before).SwingUpDown);
        Assert.IsTrue(new ACCommand.SwingLeftRight(true).Apply(before).SwingLeftRight);
    }
}
