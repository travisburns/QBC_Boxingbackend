using System.Net;
using System.Text;

namespace QBC.Api.Tests.Infrastructure;

/// <summary>
/// A test double for HttpClient's transport: returns pre-queued responses in
/// order and records every outgoing request (method, uri, body) so the gateway's
/// request shaping can be asserted without touching the network.
/// </summary>
public sealed class QueuedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<(HttpMethod Method, string Uri, string Body)> Requests { get; } = new();

    public QueuedHttpMessageHandler Enqueue(HttpStatusCode status, string json)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!.ToString(), body));

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
    }
}
