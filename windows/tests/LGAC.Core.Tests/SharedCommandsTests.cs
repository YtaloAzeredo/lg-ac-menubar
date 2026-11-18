using System.Text.Json;
using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class SharedCommandsTests
{
    private static JsonArray Cases()
    {
        return Fixture.Json("commands").AsJsonArray() ?? throw new InvalidDataException("commands.json não é um array");
    }

    private static ACCommand CommandFrom(JsonNode? spec)
    {
        var type = spec.Get("type").AsString();
        var value = spec.Get("value");
        var kind = (value as JsonValue)?.GetValueKind();
        return (type, kind) switch
        {
            ("power", JsonValueKind.True or JsonValueKind.False) => new ACCommand.Power(value.AsBool()!.Value),
            ("jobMode", JsonValueKind.String) => new ACCommand.JobMode(value.AsString()!),
            ("targetTemp", JsonValueKind.Number) => new ACCommand.TargetTemp(value.AsDouble()!.Value),
            ("windStrength", JsonValueKind.String) => new ACCommand.WindStrength(value.AsString()!),
            ("swingUpDown", JsonValueKind.True or JsonValueKind.False) => new ACCommand.SwingUpDown(value.AsBool()!.Value),
            ("swingLeftRight", JsonValueKind.True or JsonValueKind.False) => new ACCommand.SwingLeftRight(value.AsBool()!.Value),
            ("offTimer", JsonValueKind.Number) => new ACCommand.OffTimer(value.AsInt()),
            ("offTimer", null) => new ACCommand.OffTimer(null),
            ("displayLight", JsonValueKind.True or JsonValueKind.False) => new ACCommand.DisplayLight(value.AsBool()!.Value),
            _ => throw new InvalidDataException($"comando desconhecido: {type}"),
        };
    }

    [TestMethod]
    public void EverySharedCasePayloadMatches()
    {
        foreach (var item in Cases())
        {
            var name = item.Get("name").AsString();
            var caps = new ACCapabilities(Fixture.Json(item.Get("profile").AsString()!));
            var command = CommandFrom(item.Get("command"));
            var actual = command.Payload(caps);
            var expected = item.Get("payload");
            Assert.IsTrue(
                JsonNode.DeepEquals(expected, actual),
                $"caso \"{name}\": payload diferente do esperado. Esperado: {expected?.ToJsonString() ?? "null"}. Obtido: {actual?.ToJsonString() ?? "null"}");
        }
    }

    [TestMethod]
    public void SharedCasesCoverEveryCommandType()
    {
        var types = Cases().Select(item => item.Get("command").Get("type").AsString()).OfType<string>().ToHashSet();
        var all = new HashSet<string>
        {
            "power", "jobMode", "targetTemp", "windStrength",
            "swingUpDown", "swingLeftRight", "offTimer", "displayLight",
        };
        Assert.IsTrue(
            types.SetEquals(all),
            $"tipos ausentes ou desconhecidos: {string.Join(", ", types.Except(all).Union(all.Except(types)).Order())}");
    }
}
