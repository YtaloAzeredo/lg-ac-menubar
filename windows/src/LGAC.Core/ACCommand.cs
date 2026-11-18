using System.Text.Json.Nodes;

namespace LGAC.Core;

public abstract record ACCommand
{
    private ACCommand()
    {
    }

    public sealed record Power(bool On) : ACCommand;

    public sealed record JobMode(string Mode) : ACCommand;

    public sealed record TargetTemp(double Value) : ACCommand;

    public sealed record WindStrength(string Value) : ACCommand;

    public sealed record SwingUpDown(bool On) : ACCommand;

    public sealed record SwingLeftRight(bool On) : ACCommand;

    public sealed record OffTimer(int? Minutes) : ACCommand;

    public sealed record DisplayLight(bool On) : ACCommand;

    public JsonObject? Payload(ACCapabilities caps)
    {
        switch (this)
        {
            case Power power:
                if (!caps.Power)
                {
                    return null;
                }

                return Wrap("operation", new JsonObject { ["airConOperationMode"] = power.On ? "POWER_ON" : "POWER_OFF" });

            case JobMode jobMode:
                if (!caps.JobModes.Contains(jobMode.Mode))
                {
                    return null;
                }

                return Wrap("airConJobMode", new JsonObject { ["currentJobMode"] = jobMode.Mode });

            case TargetTemp targetTemp:
                if (caps.TargetTemp is not { } temp)
                {
                    return null;
                }

                var body = new JsonObject { [temp.Key] = temp.Clamp(targetTemp.Value) };
                if (temp.Resource == "temperatureInUnits")
                {
                    body["unit"] = "C";
                }

                return Wrap(temp.Resource, body);

            case WindStrength windStrength:
                if (!caps.WindStrengths.Contains(windStrength.Value))
                {
                    return null;
                }

                return Wrap("airFlow", new JsonObject { [caps.WindStrengthKey] = windStrength.Value });

            case SwingUpDown swingUpDown:
                if (!caps.SwingUpDown)
                {
                    return null;
                }

                return Wrap("windDirection", new JsonObject { ["rotateUpDown"] = swingUpDown.On });

            case SwingLeftRight swingLeftRight:
                if (!caps.SwingLeftRight)
                {
                    return null;
                }

                return Wrap("windDirection", new JsonObject { ["rotateLeftRight"] = swingLeftRight.On });

            case OffTimer offTimer:
                if (caps.OffTimer is not { } kind)
                {
                    return null;
                }

                if (offTimer.Minutes is not { } minutes || minutes <= 0)
                {
                    return Wrap(kind.JsonKey(), new JsonObject { ["relativeStopTimer"] = "UNSET" });
                }

                var timer = new JsonObject { ["relativeHourToStop"] = minutes / 60 };
                if (minutes % 60 != 0)
                {
                    timer["relativeMinuteToStop"] = minutes % 60;
                }

                return Wrap(kind.JsonKey(), timer);

            case DisplayLight displayLight:
                if (caps.DisplayLight is not { } lightKind)
                {
                    return null;
                }

                JsonNode light = lightKind == DisplayLightKind.Boolean
                    ? JsonValue.Create(displayLight.On)
                    : JsonValue.Create(displayLight.On ? "ON" : "OFF")!;
                return Wrap("display", new JsonObject { ["light"] = light });

            default:
                return null;
        }
    }

    public ACState Apply(ACState state)
    {
        return this switch
        {
            Power power => state with { IsOn = power.On },
            JobMode jobMode => state with { JobMode = jobMode.Mode },
            TargetTemp targetTemp => state with { TargetTemp = targetTemp.Value },
            WindStrength windStrength => state with { WindStrength = windStrength.Value },
            SwingUpDown swingUpDown => state with { SwingUpDown = swingUpDown.On },
            SwingLeftRight swingLeftRight => state with { SwingLeftRight = swingLeftRight.On },
            OffTimer offTimer => state with { OffTimerRemainingMinutes = (offTimer.Minutes ?? 0) > 0 ? offTimer.Minutes : null },
            DisplayLight displayLight => state with { DisplayLightOn = displayLight.On },
            _ => state,
        };
    }

    private static JsonObject Wrap(string key, JsonObject body)
    {
        return new JsonObject { [key] = body };
    }
}
