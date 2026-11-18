using System.Net;
using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ThinQClientTests
{
    private static readonly ThinQCredentials Credentials = new("test-token", "BR", "lgac-test");

    private static ThinQClient MakeClient(StubHandler handler, ThinQCredentials? credentials = null)
    {
        return new ThinQClient(credentials ?? Credentials, handler);
    }

    private static string? Header(RecordedRequest request, string name)
    {
        if (request.Message.Headers.TryGetValues(name, out var values))
        {
            return values.Single();
        }

        if (request.Message.Content is not null && request.Message.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return contentValues.Single();
        }

        return null;
    }

    [TestMethod]
    public async Task ListsDevicesWithAllHeaders()
    {
        var handler = StubHandler.Responding(HttpStatusCode.OK, """
            {"response": [
              {"deviceId": "ac-1", "deviceInfo": {"deviceType": "DEVICE_AIR_CONDITIONER", "modelName": "M1", "alias": "Ar do Quarto", "reportable": true}},
              {"deviceId": "w-1", "deviceInfo": {"deviceType": "DEVICE_WASHER", "modelName": "W1", "alias": "Lavadora"}}
            ]}
            """);

        var devices = await MakeClient(handler).DevicesAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                new ThinQDevice("ac-1", "DEVICE_AIR_CONDITIONER", "Ar do Quarto", "M1"),
                new ThinQDevice("w-1", "DEVICE_WASHER", "Lavadora", "W1"),
            },
            devices.ToArray());
        CollectionAssert.AreEqual(new[] { true, false }, devices.Select(device => device.IsAirConditioner).ToArray());

        var request = handler.Requests.Single();
        Assert.AreEqual(HttpMethod.Get, request.Message.Method);
        Assert.AreEqual("https://api-aic.lgthinq.com/devices", request.Message.RequestUri!.AbsoluteUri);
        Assert.AreEqual("Bearer test-token", Header(request, "Authorization"));
        Assert.AreEqual("BR", Header(request, "x-country"));
        Assert.AreEqual("lgac-test", Header(request, "x-client-id"));
        Assert.AreEqual(ThinQClient.ApiKey, Header(request, "x-api-key"));
        Assert.AreEqual("OP", Header(request, "x-service-phase"));
        Assert.IsNull(Header(request, "x-conditional-control"));
        var messageId = Header(request, "x-message-id");
        Assert.IsNotNull(messageId);
        Assert.AreEqual(22, messageId.Length);
        Assert.IsTrue(messageId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
    }

    [TestMethod]
    public void MessageIdsAreUnique()
    {
        Assert.AreNotEqual(ThinQClient.MakeMessageId(), ThinQClient.MakeMessageId());
    }

    [TestMethod]
    public async Task ProfileAndStateReturnResponseContent()
    {
        var handler = StubHandler.Responding(HttpStatusCode.OK, """{"response": {"property": {"x": 1}}}""");
        var client = MakeClient(handler);

        var profile = await client.ProfileAsync("ac-1");
        await client.StateAsync("ac-1");

        Assert.AreEqual(1, profile.Get("property").Get("x").AsInt());
        CollectionAssert.AreEqual(
            new[]
            {
                "https://api-aic.lgthinq.com/devices/ac-1/profile",
                "https://api-aic.lgthinq.com/devices/ac-1/state",
            },
            handler.Requests.Select(request => request.Message.RequestUri!.AbsoluteUri).ToArray());
    }

    [TestMethod]
    public async Task ControlPostsBodyWithConditionalHeader()
    {
        var handler = StubHandler.Responding(HttpStatusCode.OK, """{"response": {}}""");
        var payload = JsonNode.Parse("""{"display": {"light": "OFF"}}""")!;

        await MakeClient(handler).ControlAsync("ac-1", payload);

        var request = handler.Requests.Single();
        Assert.AreEqual(HttpMethod.Post, request.Message.Method);
        Assert.AreEqual("https://api-aic.lgthinq.com/devices/ac-1/control", request.Message.RequestUri!.AbsoluteUri);
        Assert.AreEqual("true", Header(request, "x-conditional-control"));
        Assert.AreEqual("application/json", request.Message.Content!.Headers.ContentType!.MediaType);
        Assert.IsTrue(JsonNode.DeepEquals(payload, JsonNode.Parse(request.Body!)));
    }

    [TestMethod]
    public async Task ErrorBodyBecomesThinQException()
    {
        var client = MakeClient(StubHandler.Responding(HttpStatusCode.BadRequest, """{"error": {"code": "1306", "message": "limit"}}"""));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.StateAsync("ac-1"));

        Assert.AreEqual(new ThinQException(ThinQErrorKind.RateLimited), error);
    }

    [TestMethod]
    public async Task NumericErrorCodeIsAccepted()
    {
        var client = MakeClient(StubHandler.Responding(HttpStatusCode.BadRequest, """{"error": {"code": 1222, "message": "offline"}}"""));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.StateAsync("ac-1"));

        Assert.AreEqual(new ThinQException(ThinQErrorKind.DeviceOffline), error);
    }

    [TestMethod]
    public async Task NonJsonErrorBodyDoesNotThrowParsingException()
    {
        var client = MakeClient(StubHandler.Responding(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>"));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.StateAsync("ac-1"));

        Assert.AreEqual(new ThinQException(ThinQErrorKind.Unexpected, "HTTP 502"), error);
    }

    [TestMethod]
    public async Task TransportFailureBecomesNetworkError()
    {
        var client = MakeClient(StubHandler.Throwing(new HttpRequestException("falha de rede")));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.DevicesAsync());

        Assert.AreEqual(ThinQErrorKind.Network, error.Kind);
    }

    [TestMethod]
    public async Task CallerCancellationPropagates()
    {
        var client = MakeClient(StubHandler.Responding(HttpStatusCode.OK, """{"response": []}"""));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.DevicesAsync(source.Token));
    }

    [TestMethod]
    public void UnsupportedCountryIsRejected()
    {
        var error = Assert.ThrowsExactly<ThinQException>(() => new ThinQClient(new ThinQCredentials("t", "XX", "c")));

        Assert.AreEqual(ThinQErrorKind.UnsupportedCountry, error.Kind);
    }

    [TestMethod]
    public async Task ServerFollowsCountry()
    {
        var handler = StubHandler.Responding(HttpStatusCode.OK, """{"response": []}""");

        await MakeClient(handler, new ThinQCredentials("t", "DE", "c")).DevicesAsync();

        Assert.AreEqual("api-eic.lgthinq.com", handler.Requests.Single().Message.RequestUri!.Host);
    }

    [TestMethod]
    public async Task NonJsonErrorBodyBecomesUnexpected()
    {
        var client = MakeClient(StubHandler.Responding(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>"));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.ProfileAsync("ac-1"));

        Assert.AreEqual(ThinQErrorKind.Unexpected, error.Kind);
        Assert.AreEqual("Erro LG HTTP 502", error.UserMessage);
    }

    [TestMethod]
    public async Task EmptySuccessBodyIsNull()
    {
        var client = MakeClient(StubHandler.Responding(HttpStatusCode.OK, ""));

        Assert.IsNull(await client.ProfileAsync("ac-1"));
        Assert.IsNull(await client.StateAsync("ac-1"));
    }

    [TestMethod]
    public async Task TimeoutBecomesNetworkError()
    {
        var client = MakeClient(StubHandler.Throwing(new TaskCanceledException("timeout", new TimeoutException())));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.DevicesAsync());

        Assert.AreEqual(ThinQErrorKind.Network, error.Kind);
    }

    [TestMethod]
    public async Task ExceptionMessageNeverContainsToken()
    {
        const string token = "secret-token-123";
        var credentials = new ThinQCredentials(token, "BR", "lgac-test");
        var handlers = new[]
        {
            StubHandler.Responding(HttpStatusCode.Unauthorized, ""),
            StubHandler.Responding(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>"),
            StubHandler.Responding(HttpStatusCode.BadRequest, """{"error": {"code": "9999", "message": "falhou"}}"""),
            StubHandler.Throwing(new HttpRequestException($"Bearer {token}")),
            StubHandler.Throwing(new TaskCanceledException("timeout", new TimeoutException())),
        };

        foreach (var handler in handlers)
        {
            var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => MakeClient(handler, credentials).DevicesAsync());
            Assert.DoesNotContain(token, error.Message);
            Assert.DoesNotContain(token, error.ToString());
        }
    }

    [TestMethod]
    public void CredentialsToStringOmitsToken()
    {
        var text = new ThinQCredentials("secret-token-123", "BR", "lgac-test").ToString();

        Assert.DoesNotContain("secret-token-123", text);
        Assert.Contains("BR", text);
    }

    [TestMethod]
    [DataRow("abc\ndef")]
    [DataRow("abc def")]
    [DataRow("abc…def")]
    [DataRow("")]
    public async Task MalformedTokenBecomesInvalidToken(string token)
    {
        var handler = StubHandler.Responding(HttpStatusCode.OK, """{"response": []}""");
        var client = MakeClient(handler, new ThinQCredentials(token, "BR", "lgac-test"));

        var error = await Assert.ThrowsExactlyAsync<ThinQException>(() => client.DevicesAsync());

        Assert.AreEqual(ThinQErrorKind.InvalidToken, error.Kind);
        Assert.IsEmpty(handler.Requests);
        Assert.DoesNotContain("abc", error.Message);
    }

    [TestMethod]
    public async Task DeviceIdIsEscapedInPath()
    {
        var handler = StubHandler.Responding(HttpStatusCode.OK, """{"response": {}}""");
        var client = MakeClient(handler);

        await client.StateAsync("a b/c?d");

        var uri = handler.Requests.Single().Message.RequestUri!;
        Assert.AreEqual("https://api-aic.lgthinq.com/devices/a%20b%2Fc%3Fd/state", uri.AbsoluteUri);
        Assert.IsEmpty(uri.Query);
    }
}
