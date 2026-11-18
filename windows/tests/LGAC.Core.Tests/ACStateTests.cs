using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACStateTests
{
    [TestMethod]
    public void FullState()
    {
        var state = ACState.FromJson(Fixture.Json("state-full"));
        Assert.AreEqual(
            new ACState
            {
                IsOn = true,
                JobMode = "COOL",
                TargetTemp = 23,
                CurrentTemp = 25,
                WindStrength = "MID",
                SwingUpDown = false,
                SwingLeftRight = true,
                DisplayLightOn = true,
                OffTimerRemainingMinutes = 80,
            },
            state);
    }

    [TestMethod]
    public void BasicState()
    {
        var state = ACState.FromJson(Fixture.Json("state-basic"));
        Assert.AreEqual(
            new ACState
            {
                IsOn = false,
                JobMode = "FAN",
                TargetTemp = 22.5,
                CurrentTemp = 27.5,
                WindStrength = "HIGH",
                SwingUpDown = true,
            },
            state);
    }

    [TestMethod]
    public void PrefersWindStrengthOverDetail()
    {
        var state = ACState.FromJson(JsonNode.Parse("""
            {"airFlow": {"windStrength": "AUTO", "windStrengthDetail": "NATURE"}}
            """));
        Assert.AreEqual("AUTO", state.WindStrength);
    }

    [TestMethod]
    public void EmptyStateHasNoValues()
    {
        Assert.AreEqual(new ACState(), ACState.FromJson(JsonNode.Parse("{}")));
    }

    [TestMethod]
    public void LegacyTemperatureResource()
    {
        var state = ACState.FromJson(JsonNode.Parse("""
            {"temperature": {"currentTemperature": 26, "targetTemperature": 21, "unit": "C"}}
            """));
        Assert.AreEqual(21.0, state.TargetTemp);
        Assert.AreEqual(26.0, state.CurrentTemp);
    }

    [TestMethod]
    public void UnsetTimerIsIgnored()
    {
        var state = ACState.FromJson(JsonNode.Parse("""
            {"timer": {"relativeStopTimer": "UNSET", "relativeHourToStop": 2}}
            """));
        Assert.IsNull(state.OffTimerRemainingMinutes);
    }

    [TestMethod]
    public void SleepTimerRemaining()
    {
        var state = ACState.FromJson(JsonNode.Parse("""
            {"sleepTimer": {"relativeStopTimer": "SET", "relativeHourToStop": 0, "relativeMinuteToStop": 30}}
            """));
        Assert.AreEqual(30, state.OffTimerRemainingMinutes);
    }

    [TestMethod]
    public void BooleanLight()
    {
        var state = ACState.FromJson(JsonNode.Parse("""{"display": {"light": false}}"""));
        Assert.IsFalse(state.DisplayLightOn);
    }
}
