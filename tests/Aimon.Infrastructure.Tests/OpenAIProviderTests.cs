using System.Net;
using System.Text;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.OpenAI;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class OpenAIProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static ProviderRequest SampleRequest() => new()
    {
        Model = new ModelRef("openai", "gpt-4o"),
        Messages = [Message.User("hi")],
    };

    private static OpenAIProvider CreateProvider(HttpClient httpClient, params (string Key, string Value)[] secrets)
        => new(httpClient, new InMemorySecretStore(secrets), OpenAIProviderOptions.Default());

    [Fact]
    public async Task Streams_Text_And_Lifecycle_States_From_Recorded_Sse()
    {
        const string sse = """
            data: {"choices":[{"delta":{"role":"assistant","content":""},"finish_reason":null}]}

            data: {"choices":[{"delta":{"content":"Hello"},"finish_reason":null}]}

            data: {"choices":[{"delta":{"content":" world"},"finish_reason":null}]}

            data: {"choices":[{"delta":{},"finish_reason":"stop"}]}

            data: [DONE]
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
        };
        using var handler = new StubHttpMessageHandler(response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var provider = CreateProvider(http, ("openai", "sk-test"));

        var items = await AsyncStream.ToListAsync(provider.StreamAsync(SampleRequest(), Ct));

        Assert.Equal(AgentState.Working, Assert.IsType<ProviderStreamItem.StateChange>(items[0]).State);
        Assert.Equal("Hello world", string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(delta => delta.Text)));
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "finish" });
        Assert.Equal(AgentState.Idle, Assert.IsType<ProviderStreamItem.StateChange>(items[^1]).State);
    }

    [Fact]
    public async Task Emits_Error_When_No_Api_Key_Is_Configured()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://stub.local/v1/") };
        var provider = CreateProvider(http);

        var items = await AsyncStream.ToListAsync(provider.StreamAsync(SampleRequest(), Ct));

        Assert.Equal(AgentState.Error, Assert.IsType<ProviderStreamItem.StateChange>(items[0]).State);
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "error" });
    }

    [Fact]
    public async Task Emits_Error_On_A_Non_Success_Status()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"invalid key\"}", Encoding.UTF8, "application/json"),
        };
        using var handler = new StubHttpMessageHandler(response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var provider = CreateProvider(http, ("openai", "sk-bad"));

        var items = await AsyncStream.ToListAsync(provider.StreamAsync(SampleRequest(), Ct));

        Assert.Contains(items, item => item is ProviderStreamItem.StateChange { State: AgentState.Error });
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "error" });
    }

    [Fact]
    public async Task Sends_A_Streaming_Chat_Request_To_The_Model()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: [DONE]", Encoding.UTF8, "text/event-stream"),
        };
        using var handler = new StubHttpMessageHandler(response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };
        var provider = CreateProvider(http, ("openai", "sk-test"));

        _ = await AsyncStream.ToListAsync(provider.StreamAsync(SampleRequest(), Ct));

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"model\":\"gpt-4o\"", handler.LastRequestBody);
        Assert.Contains("\"stream\":true", handler.LastRequestBody);
    }
}
