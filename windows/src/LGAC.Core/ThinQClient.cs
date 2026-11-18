using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LGAC.Core;

public sealed record ThinQDevice(string Id, string Type, string Alias, string ModelName)
{
    public bool IsAirConditioner => Type == "DEVICE_AIR_CONDITIONER";

    public static ThinQDevice? FromJson(JsonNode? json)
    {
        var id = json.Get("deviceId").AsString();
        if (id is null)
        {
            return null;
        }

        var info = json.Get("deviceInfo");
        return new ThinQDevice(
            id,
            info.Get("deviceType").AsString() ?? "",
            info.Get("alias").AsString() ?? "",
            info.Get("modelName").AsString() ?? "");
    }
}

public sealed record ThinQCredentials(string Token, string Country, string ClientId)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Country = ").Append(Country).Append(", ClientId = ").Append(ClientId);
        return true;
    }
}

public interface IThinQClient
{
    Task<IReadOnlyList<ThinQDevice>> DevicesAsync(CancellationToken ct = default);

    Task<JsonNode?> ProfileAsync(string deviceId, CancellationToken ct = default);

    Task<JsonNode?> StateAsync(string deviceId, CancellationToken ct = default);

    Task ControlAsync(string deviceId, JsonNode payload, CancellationToken ct = default);
}

public sealed class ThinQClient : IThinQClient
{
    public const string ApiKey = "v6GFvkweNo7DK7yD3ylIZ9w52aKBU0eJ7wLXkSR3";

    private readonly ThinQCredentials credentials;
    private readonly Uri baseUrl;
    private readonly HttpClient http;

    public ThinQClient(ThinQCredentials credentials, HttpMessageHandler? handler = null)
    {
        baseUrl = ThinQRegion.BaseUrlFor(credentials.Country)
            ?? throw new ThinQException(ThinQErrorKind.UnsupportedCountry);
        this.credentials = credentials;
        http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<IReadOnlyList<ThinQDevice>> DevicesAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Get, "devices", null, ct);
        return response.AsJsonArray()?
            .Select(ThinQDevice.FromJson)
            .OfType<ThinQDevice>()
            .ToList() ?? [];
    }

    public Task<JsonNode?> ProfileAsync(string deviceId, CancellationToken ct = default)
    {
        return SendAsync(HttpMethod.Get, $"devices/{Uri.EscapeDataString(deviceId)}/profile", null, ct);
    }

    public Task<JsonNode?> StateAsync(string deviceId, CancellationToken ct = default)
    {
        return SendAsync(HttpMethod.Get, $"devices/{Uri.EscapeDataString(deviceId)}/state", null, ct);
    }

    public async Task ControlAsync(string deviceId, JsonNode payload, CancellationToken ct = default)
    {
        await SendAsync(HttpMethod.Post, $"devices/{Uri.EscapeDataString(deviceId)}/control", payload, ct);
    }

    public static string MakeMessageId()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private async Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        using var request = MakeRequest(method, path, body);
        int status;
        string text;
        try
        {
            using var response = await http.SendAsync(request, ct);
            status = (int)response.StatusCode;
            text = await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException)
        {
            throw new ThinQException(ThinQErrorKind.Network);
        }

        var json = ParseJson(text);
        if (status is < 200 or >= 300)
        {
            var error = json.Get("error");
            var codeNode = error.Get("code");
            var code = codeNode.AsString() ?? codeNode.AsInt()?.ToString(CultureInfo.InvariantCulture);
            throw ThinQException.From(code, error.Get("message").AsString(), status);
        }

        return json.Get("response");
    }

    private HttpRequestMessage MakeRequest(HttpMethod method, string path, JsonNode? body)
    {
        if (!IsValidToken(credentials.Token))
        {
            throw new ThinQException(ThinQErrorKind.InvalidToken);
        }

        var request = new HttpRequestMessage(method, new Uri(baseUrl, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Token);
        request.Headers.TryAddWithoutValidation("x-country", credentials.Country.ToUpperInvariant());
        request.Headers.TryAddWithoutValidation("x-message-id", MakeMessageId());
        request.Headers.TryAddWithoutValidation("x-client-id", credentials.ClientId);
        request.Headers.TryAddWithoutValidation("x-api-key", ApiKey);
        request.Headers.TryAddWithoutValidation("x-service-phase", "OP");
        if (body is not null)
        {
            request.Headers.TryAddWithoutValidation("x-conditional-control", "true");
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static bool IsValidToken(string token)
    {
        return token.Length > 0 && token.All(c => c is > ' ' and < '\u007f');
    }

    private static JsonNode? ParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
