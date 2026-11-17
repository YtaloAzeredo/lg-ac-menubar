using System.Text.Json.Nodes;

namespace LGAC.Core;

public sealed record ACState
{
    public bool? IsOn { get; init; }

    public string? JobMode { get; init; }

    public double? TargetTemp { get; init; }

    public double? CurrentTemp { get; init; }

    public string? WindStrength { get; init; }

    public bool? SwingUpDown { get; init; }

    public bool? SwingLeftRight { get; init; }

    public bool? DisplayLightOn { get; init; }

    public int? OffTimerRemainingMinutes { get; init; }

    public static ACState FromJson(JsonNode? state)
    {
        var celsius = ACCapabilities.CelsiusEntry(state.Get("temperatureInUnits"));
        var legacy = state.Get("temperature");
        var airFlow = state.Get("airFlow");
        var mode = state.Get("operation").Get("airConOperationMode").AsString();
        return new ACState
        {
            IsOn = mode is null ? null : mode == "POWER_ON",
            JobMode = state.Get("airConJobMode").Get("currentJobMode").AsString(),
            TargetTemp = celsius.Get("targetTemperature").AsDouble() ?? legacy.Get("targetTemperature").AsDouble(),
            CurrentTemp = celsius.Get("currentTemperature").AsDouble() ?? legacy.Get("currentTemperature").AsDouble(),
            WindStrength = airFlow.Get("windStrength").AsString() ?? airFlow.Get("windStrengthDetail").AsString(),
            SwingUpDown = state.Get("windDirection").Get("rotateUpDown").AsBool(),
            SwingLeftRight = state.Get("windDirection").Get("rotateLeftRight").AsBool(),
            DisplayLightOn = LightIsOn(state.Get("display").Get("light")),
            OffTimerRemainingMinutes = RemainingMinutes(state.Get("timer")) ?? RemainingMinutes(state.Get("sleepTimer")),
        };
    }

    private static bool? LightIsOn(JsonNode? value)
    {
        if (value.AsBool() is { } on)
        {
            return on;
        }

        var text = value.AsString();
        return text is null ? null : text == "ON";
    }

    private static int? RemainingMinutes(JsonNode? timer)
    {
        if (timer is null || timer.Get("relativeStopTimer").AsString() == "UNSET")
        {
            return null;
        }

        var total = (timer.Get("relativeHourToStop").AsInt() ?? 0) * 60 + (timer.Get("relativeMinuteToStop").AsInt() ?? 0);
        return total > 0 ? total : null;
    }
}
