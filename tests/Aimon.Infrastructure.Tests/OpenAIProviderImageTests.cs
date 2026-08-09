using System.Net;
using System.Text;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.OpenAI;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>Covers image generation (auto tool-call + forced image mode) and vision input.</summary>
public class OpenAIProviderImageTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly byte[] ImageBytes = [1, 2, 3, 4];

    private static OpenAIProvider CreateProvider(HttpClient httpClient)
        => new(httpClient, new InMemorySecretStore(("openai", "sk-test")), OpenAIProviderOptions.Default());

    private static HttpResponseMessage Sse(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string ImagesJson()
        => $$"""{"data":[{"b64_json":"{{Convert.ToBase64String(ImageBytes)}}","revised_prompt":"a red fox"}]}""";

    [Fact]
    public async Task Auto_Tool_Call_Generates_An_Image_Then_Streams_A_Caption()
    {
        const string toolCallSse = """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"generate_image","arguments":""}}]},"finish_reason":null}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"prompt\":\"a fox\"}"}}]},"finish_reason":null}]}

            data: {"choices":[{"delta":{},"finish_reason":"tool_calls"}]}

            data: [DONE]
            """;

        const string captionSse = """
            data: {"choices":[{"delta":{"content":"Here is your fox."},"finish_reason":null}]}

            data: {"choices":[{"delta":{},"finish_reason":"stop"}]}

            data: [DONE]
            """;

        using var handler = new StubHttpMessageHandler(Sse(toolCallSse), Json(ImagesJson()), Sse(captionSse));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };

        var request = new ProviderRequest
        {
            Model = new ModelRef("openai", "gpt-4o"),
            Messages = [Message.User("draw a fox")],
            Mode = TurnMode.Auto,
            ImageToolEnabled = true,
        };

        var items = await AsyncStream.ToListAsync(CreateProvider(http).StreamAsync(request, Ct));

        var image = Assert.Single(items.OfType<ProviderStreamItem.ImageArtifact>());
        Assert.Equal(ImageBytes, image.Data);
        Assert.Equal("image/png", image.MediaType);

        var text = string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(delta => delta.Text));
        Assert.Contains("Here is your fox.", text);

        Assert.Contains("generate_image", handler.RequestBodies[0]);
        Assert.Contains("\"tools\"", handler.RequestBodies[0]);
        Assert.EndsWith("images/generations", handler.RequestUris[1]!.AbsolutePath);
        Assert.Equal(AgentState.Idle, Assert.IsType<ProviderStreamItem.StateChange>(items[^1]).State);
    }

    [Fact]
    public async Task Image_Mode_Calls_The_Images_Endpoint_Directly()
    {
        using var handler = new StubHttpMessageHandler(Json(ImagesJson()));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };

        var request = new ProviderRequest
        {
            Model = new ModelRef("openai", "gpt-4o"),
            Messages = [Message.User("a blue car")],
            Mode = TurnMode.Image,
        };

        var items = await AsyncStream.ToListAsync(CreateProvider(http).StreamAsync(request, Ct));

        var image = Assert.Single(items.OfType<ProviderStreamItem.ImageArtifact>());
        Assert.Equal(ImageBytes, image.Data);

        Assert.Single(handler.RequestBodies); // no chat call was made
        Assert.EndsWith("images/generations", handler.RequestUris[0]!.AbsolutePath);
        Assert.Contains("\"model\":\"gpt-image-1\"", handler.RequestBodies[0]);
        Assert.Contains("\"prompt\":\"a blue car\"", handler.RequestBodies[0]);
    }

    [Fact]
    public async Task Vision_Input_Is_Sent_As_Multimodal_Content_Parts()
    {
        using var handler = new StubHttpMessageHandler(Sse("data: [DONE]"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://stub.local/v1/") };

        var attachment = new ImageAttachment([1, 2, 3], "image/png");
        var request = new ProviderRequest
        {
            Model = new ModelRef("openai", "gpt-4o"),
            Messages = [Message.User("what is this?", [attachment])],
            Mode = TurnMode.Chat,
        };

        _ = await AsyncStream.ToListAsync(CreateProvider(http).StreamAsync(request, Ct));

        Assert.Contains("\"type\":\"text\"", handler.RequestBodies[0]);
        Assert.Contains("\"type\":\"image_url\"", handler.RequestBodies[0]);
        Assert.Contains("data:image/png;base64,AQID", handler.RequestBodies[0]);
        Assert.Contains("what is this?", handler.RequestBodies[0]);
    }
}
