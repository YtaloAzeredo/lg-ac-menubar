using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class JsonTests
{
    [TestMethod]
    public void MissingKeyIsNull()
    {
        var node = JsonNode.Parse("""{"a": 1}""");
        Assert.IsNull(node.Get("b"));
        Assert.IsNull(node.Get("b").AsString());
        Assert.IsNull(((JsonNode?)null).Get("a"));
    }

    [TestMethod]
    public void WrongTypeIsNull()
    {
        var node = JsonNode.Parse("""{"s": "text", "n": 5, "b": true}""");
        Assert.IsNull(node.Get("s").AsDouble());
        Assert.IsNull(node.Get("s").AsInt());
        Assert.IsNull(node.Get("n").AsString());
        Assert.IsNull(node.Get("n").AsBool());
        Assert.IsNull(node.Get("b").AsInt());
        Assert.IsNull(node.Get("n").AsJsonArray());
    }

    [TestMethod]
    public void IntegerValuedDoubleIsInt()
    {
        var node = JsonNode.Parse("""{"a": 22.0, "b": 22}""");
        Assert.AreEqual(22, node.Get("a").AsInt());
        Assert.AreEqual(22, node.Get("b").AsInt());
        Assert.AreEqual(22.0, node.Get("b").AsDouble());
    }

    [TestMethod]
    public void ArrayIsReturned()
    {
        var node = JsonNode.Parse("""{"a": [1, 2]}""");
        Assert.AreEqual(2, node.Get("a").AsJsonArray()?.Count);
    }

    [TestMethod]
    public void AsIntTruncatesLikeSwift()
    {
        var node = JsonNode.Parse("""{"a": 22.5, "b": -22.5}""");
        Assert.AreEqual(22, node.Get("a").AsInt());
        Assert.AreEqual(-22, node.Get("b").AsInt());
    }

    [TestMethod]
    public void AsBoolReturnsRealValues()
    {
        var node = JsonNode.Parse("""{"t": true, "f": false, "n": 1, "s": "true"}""");
        Assert.IsTrue(node.Get("t").AsBool());
        Assert.IsFalse(node.Get("f").AsBool());
        Assert.IsNull(node.Get("n").AsBool());
        Assert.IsNull(node.Get("s").AsBool());
    }

    [TestMethod]
    public void ConstructedNumbersBehaveLikeParsed()
    {
        Assert.AreEqual(5.0, JsonValue.Create(5).AsDouble());
        Assert.AreEqual(5.0, JsonValue.Create(5L).AsDouble());
        Assert.AreEqual(22.5, JsonValue.Create(22.5).AsDouble());
        Assert.AreEqual(2.5, JsonValue.Create(2.5f).AsDouble());
        Assert.AreEqual(1.5, JsonValue.Create(1.5m).AsDouble());
        Assert.AreEqual(5, JsonValue.Create(5).AsInt());
        Assert.AreEqual(5, JsonValue.Create(5L).AsInt());
        Assert.AreEqual(22, JsonValue.Create(22.5).AsInt());
        Assert.AreEqual(2, JsonValue.Create(2.5f).AsInt());
        Assert.AreEqual(1, JsonValue.Create(1.5m).AsInt());
        Assert.IsNull(JsonValue.Create("5").AsDouble());
        Assert.IsNull(JsonValue.Create(true).AsInt());
    }

    [TestMethod]
    public void ConstructedStringsAndBoolsBehaveLikeParsed()
    {
        Assert.AreEqual("x", JsonValue.Create("x").AsString());
        Assert.AreEqual("", JsonValue.Create("").AsString());
        Assert.IsNull(JsonValue.Create(5).AsString());
        Assert.IsNull(JsonValue.Create(true).AsString());
        Assert.IsTrue(JsonValue.Create(true).AsBool());
        Assert.IsFalse(JsonValue.Create(false).AsBool());
        Assert.IsNull(JsonValue.Create(1).AsBool());
        Assert.IsNull(JsonValue.Create("true").AsBool());
    }

    [TestMethod]
    public void ConstructedObjectsWork()
    {
        var node = new JsonObject { ["a"] = 3, ["b"] = new JsonArray(1, 2) };
        Assert.AreEqual(3, node.Get("a").AsInt());
        Assert.AreEqual(2, node.Get("b").AsJsonArray()?.Count);
    }

    [TestMethod]
    public void FixtureIsLoadable()
    {
        Assert.IsNotNull(Fixture.Json("profile-full"));
    }
}
