using System.Text.Json.Nodes;
using LGAC.Core;

namespace LGAC.Linux;

internal sealed class SmokeClient : IThinQClient
{
    internal const string DeviceId = "smoke-ac";

    private const string Profile = """
        {"property":{
          "airConJobMode":{"currentJobMode":{"mode":["r","w"],"type":"enum","value":{"r":["COOL","FAN"],"w":["COOL","FAN"]}}},
          "operation":{"airConOperationMode":{"mode":["r","w"],"type":"enum","value":{"r":["POWER_ON","POWER_OFF"],"w":["POWER_ON","POWER_OFF"]}}},
          "temperatureInUnits":[{
            "currentTemperature":{"type":"number","mode":["r"]},
            "targetTemperature":{"type":"range","mode":["r","w"],"value":{"w":{"max":30,"min":18,"step":1}}},
            "unit":"C"}]
        }}
        """;

    private const string State = """
        {"airConJobMode":{"currentJobMode":"COOL"},
         "operation":{"airConOperationMode":"POWER_ON"},
         "temperatureInUnits":[{"currentTemperature":25,"targetTemperature":23,"unit":"C"}]}
        """;

    public Task<IReadOnlyList<ThinQDevice>> DevicesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ThinQDevice> devices = [new ThinQDevice(DeviceId, "DEVICE_AIR_CONDITIONER", "Ar de teste", "SMOKE")];
        return Task.FromResult(devices);
    }

    public Task<JsonNode?> ProfileAsync(string deviceId, CancellationToken ct = default)
    {
        return Task.FromResult(JsonNode.Parse(Profile));
    }

    public Task<JsonNode?> StateAsync(string deviceId, CancellationToken ct = default)
    {
        return Task.FromResult(JsonNode.Parse(State));
    }

    public Task ControlAsync(string deviceId, JsonNode payload, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class SmokeCredentialStore : ICredentialStore
{
    private string? token = "smoke";

    public string? Token => token;

    public string Country { get; set; } = "BR";

    public string? DeviceId { get; set; } = SmokeClient.DeviceId;

    public string? DeviceAlias { get; set; } = "Ar de teste";

    public string ClientId => "lgac-smoke";

    public void SaveToken(string value)
    {
        token = value;
    }

    public void DeleteToken()
    {
        token = null;
    }
}
