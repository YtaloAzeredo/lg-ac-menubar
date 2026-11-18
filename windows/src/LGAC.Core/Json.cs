using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LGAC.Core;

public static class Json
{
    public static JsonNode? Get(this JsonNode? node, string key)
    {
        if (node is JsonObject obj && obj.TryGetPropertyValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    public static string? AsString(this JsonNode? node)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            return value.GetValue<string>();
        }

        return null;
    }

    public static double? AsDouble(this JsonNode? node)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number)
        {
            return double.TryParse(
                value.ToJsonString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number)
                ? number
                : null;
        }

        return null;
    }

    public static int? AsInt(this JsonNode? node)
    {
        var number = node.AsDouble();
        if (number is not { } value || !double.IsFinite(value))
        {
            return null;
        }

        var truncated = Math.Truncate(value);
        return truncated is >= int.MinValue and <= int.MaxValue ? (int)truncated : null;
    }

    public static bool? AsBool(this JsonNode? node)
    {
        if (node is JsonValue value)
        {
            return value.GetValueKind() switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        return null;
    }

    public static JsonArray? AsJsonArray(this JsonNode? node)
    {
        return node as JsonArray;
    }
}
