namespace Aimon.Infrastructure.Tests.TestSupport;

/// <summary>
/// Returns canned <see cref="HttpResponseMessage"/>s so provider tests need no network. A single
/// response is reused for every call; multiple responses are handed out in order (the last one
/// repeats), which lets a multi-request flow — chat → images → chat — be scripted. Every request's
/// URI and body are captured for assertions.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public StubHttpMessageHandler(HttpResponseMessage response) => _responses = new Queue<HttpResponseMessage>([response]);

    public StubHttpMessageHandler(params HttpResponseMessage[] responses) => _responses = new Queue<HttpResponseMessage>(responses);

    public List<string> RequestBodies { get; } = [];

    public List<Uri?> RequestUris { get; } = [];

    public string? LastRequestBody => RequestBodies.Count > 0 ? RequestBodies[^1] : null;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        RequestUris.Add(request.RequestUri);

        return _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
    }
}
