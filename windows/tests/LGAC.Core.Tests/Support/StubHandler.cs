using System.Net;
using System.Text;

namespace LGAC.Core.Tests.Support;

public sealed record RecordedRequest(HttpRequestMessage Message, string? Body);

public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
    private readonly List<RecordedRequest> requests = [];

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        this.responder = responder;
    }

    public IReadOnlyList<RecordedRequest> Requests => requests;

    public static StubHandler Responding(HttpStatusCode status, string body)
    {
        return new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    public static StubHandler Throwing(Exception exception)
    {
        return new StubHandler(_ => throw exception);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        requests.Add(new RecordedRequest(request, body));
        cancellationToken.ThrowIfCancellationRequested();
        return responder(request);
    }
}
