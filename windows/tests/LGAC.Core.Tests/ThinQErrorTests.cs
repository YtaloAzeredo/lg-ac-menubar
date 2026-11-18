namespace LGAC.Core.Tests;

[TestClass]
public class ThinQErrorTests
{
    [TestMethod]
    [DataRow("1103")]
    [DataRow("1218")]
    [DataRow("1302")]
    public void TokenCodes(string code)
    {
        Assert.AreEqual(ThinQException.From(code, "x", 400), new ThinQException(ThinQErrorKind.InvalidToken));
    }

    [TestMethod]
    public void Http401WithoutBodyIsInvalidToken()
    {
        Assert.AreEqual(ThinQErrorKind.InvalidToken, ThinQException.From(null, null, 401).Kind);
    }

    [TestMethod]
    public void RateLimit()
    {
        Assert.AreEqual(ThinQErrorKind.RateLimited, ThinQException.From("1306", "x", 429).Kind);
    }

    [TestMethod]
    public void DeviceOffline()
    {
        Assert.AreEqual(ThinQErrorKind.DeviceOffline, ThinQException.From("1222", "x", 400).Kind);
    }

    [TestMethod]
    [DataRow("1102")]
    [DataRow("2205")]
    [DataRow("2207")]
    [DataRow("2208")]
    [DataRow("2301")]
    [DataRow("2302")]
    [DataRow("2303")]
    [DataRow("2304")]
    [DataRow("2305")]
    public void RejectedCommands(string code)
    {
        Assert.AreEqual(
            new ThinQException(ThinQErrorKind.CommandRejected, code, "não pode"),
            ThinQException.From(code, "não pode", 400));
    }

    [TestMethod]
    public void UnknownCode()
    {
        Assert.AreEqual(
            new ThinQException(ThinQErrorKind.Unexpected, "9999", "m"),
            ThinQException.From("9999", "m", 500));
    }

    [TestMethod]
    public void NoBodyUsesHttpStatus()
    {
        Assert.AreEqual(
            new ThinQException(ThinQErrorKind.Unexpected, "HTTP 502", ""),
            ThinQException.From(null, null, 502));
    }

    [TestMethod]
    public void UserMessages()
    {
        Assert.AreEqual("Sem conexão", new ThinQException(ThinQErrorKind.Network).UserMessage);
        Assert.AreEqual("Token inválido, gere um novo", new ThinQException(ThinQErrorKind.InvalidToken).UserMessage);
        Assert.AreEqual("Ar-condicionado offline", new ThinQException(ThinQErrorKind.DeviceOffline).UserMessage);
        Assert.AreEqual(
            "Comando recusado pelo aparelho",
            new ThinQException(ThinQErrorKind.CommandRejected, "2304", "").UserMessage);
        Assert.AreEqual(
            "Comando recusado: Desligado",
            new ThinQException(ThinQErrorKind.CommandRejected, "2304", "Desligado").UserMessage);
    }
}
