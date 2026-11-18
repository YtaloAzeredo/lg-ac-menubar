using System.Text.Json.Nodes;

namespace LGAC.Core;

public sealed record TemperatureControl(string Resource, string Key, double Min, double Max, double Step)
{
    public double Clamp(double value)
    {
        var stepped = Math.Round(value / Step, MidpointRounding.AwayFromZero) * Step;
        return Math.Min(Max, Math.Max(Min, stepped));
    }
}

public enum OffTimerKind
{
    Timer,
    SleepTimer,
}

public enum DisplayLightKind
{
    OnOff,
    Boolean,
}

public static class OffTimerKindExtensions
{
    public static string JsonKey(this OffTimerKind kind)
    {
        return kind == OffTimerKind.Timer ? "timer" : "sleepTimer";
    }
}

public sealed class ACCapabilities : IEquatable<ACCapabilities>
{
    public static readonly string[] SupportedJobModes = ["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"];

    public ACCapabilities(JsonNode? profile)
    {
        var property = profile.Get("property");

        Power = WritableEnum(property.Get("operation").Get("airConOperationMode")).Contains("POWER_ON");

        var modes = WritableEnum(property.Get("airConJobMode").Get("currentJobMode"));
        JobModes = SupportedJobModes.Where(modes.Contains).ToList();

        TargetTemp = TemperatureControlIn(property);

        var strength = property.Get("airFlow").Get("windStrength");
        if (IsWritable(strength))
        {
            WindStrengthKey = "windStrength";
            WindStrengths = WritableEnum(strength);
        }
        else
        {
            WindStrengthKey = "windStrengthDetail";
            WindStrengths = WritableEnum(property.Get("airFlow").Get("windStrengthDetail"));
        }

        SwingUpDown = IsWritable(property.Get("windDirection").Get("rotateUpDown"));
        SwingLeftRight = IsWritable(property.Get("windDirection").Get("rotateLeftRight"));

        if (IsWritable(property.Get("timer").Get("relativeHourToStop")))
        {
            OffTimer = OffTimerKind.Timer;
        }
        else if (IsWritable(property.Get("sleepTimer").Get("relativeHourToStop")))
        {
            OffTimer = OffTimerKind.SleepTimer;
        }

        var light = property.Get("display").Get("light");
        if (IsWritable(light))
        {
            DisplayLight = light.Get("type").AsString() == "boolean" ? DisplayLightKind.Boolean : DisplayLightKind.OnOff;
        }
    }

    public bool Power { get; }

    public IReadOnlyList<string> JobModes { get; }

    public TemperatureControl? TargetTemp { get; }

    public IReadOnlyList<string> WindStrengths { get; }

    public string WindStrengthKey { get; }

    public bool SwingUpDown { get; }

    public bool SwingLeftRight { get; }

    public OffTimerKind? OffTimer { get; }

    public DisplayLightKind? DisplayLight { get; }

    public bool Equals(ACCapabilities? other)
    {
        return other is not null
            && Power == other.Power
            && JobModes.SequenceEqual(other.JobModes)
            && Equals(TargetTemp, other.TargetTemp)
            && WindStrengths.SequenceEqual(other.WindStrengths)
            && WindStrengthKey == other.WindStrengthKey
            && SwingUpDown == other.SwingUpDown
            && SwingLeftRight == other.SwingLeftRight
            && OffTimer == other.OffTimer
            && DisplayLight == other.DisplayLight;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ACCapabilities);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Power);
        foreach (var mode in JobModes)
        {
            hash.Add(mode);
        }

        hash.Add(TargetTemp);
        foreach (var strength in WindStrengths)
        {
            hash.Add(strength);
        }

        hash.Add(WindStrengthKey);
        hash.Add(SwingUpDown);
        hash.Add(SwingLeftRight);
        hash.Add(OffTimer);
        hash.Add(DisplayLight);
        return hash.ToHashCode();
    }

    internal static bool IsWritable(JsonNode? entry)
    {
        var modes = entry.Get("mode").AsJsonArray();
        return modes is not null && modes.Any(mode => mode.AsString() == "w");
    }

    internal static List<string> WritableEnum(JsonNode? entry)
    {
        if (!IsWritable(entry))
        {
            return [];
        }

        var values = entry.Get("value").Get("w").AsJsonArray();
        if (values is null)
        {
            return [];
        }

        return values.Select(value => value.AsString()).OfType<string>().ToList();
    }

    internal static JsonNode? CelsiusEntry(JsonNode? resource)
    {
        var items = resource.AsJsonArray();
        return items?.FirstOrDefault(item => item.Get("unit").AsString() == "C");
    }

    private static TemperatureControl? TemperatureControlIn(JsonNode? property)
    {
        var inUnits = CelsiusEntry(property.Get("temperatureInUnits"));
        var legacy = property.Get("temperature");
        (string Resource, string Key, JsonNode? Entry)[] candidates =
        [
            ("temperatureInUnits", "targetTemperature", inUnits.Get("targetTemperature")),
            ("temperature", "targetTemperature", legacy.Get("targetTemperature")),
            ("temperatureInUnits", "coolTargetTemperature", inUnits.Get("coolTargetTemperature")),
            ("temperature", "coolTargetTemperature", legacy.Get("coolTargetTemperature")),
        ];

        foreach (var (resource, key, entry) in candidates)
        {
            if (!IsWritable(entry))
            {
                continue;
            }

            var range = entry.Get("value").Get("w");
            if (range.Get("min").AsDouble() is not { } min || range.Get("max").AsDouble() is not { } max)
            {
                continue;
            }

            var step = range.Get("step").AsDouble() ?? 1;
            return new TemperatureControl(resource, key, min, max, step > 0 ? step : 1);
        }

        return null;
    }
}
