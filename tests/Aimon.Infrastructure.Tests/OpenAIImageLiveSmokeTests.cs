using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.OpenAI;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>
/// Live smoke tests for image generation and vision against the real OpenAI API. Gated on
/// <c>AIMON_OPENAI_API_KEY</c>, so a no-op in CI and by default. Vision uses <c>AIMON_OPENAI_MODEL</c>
/// (default <c>gpt-5.4-mini</c>); generation always uses the configured image model.
/// </summary>
[Trait("Category", "Live")]
public class OpenAIImageLiveSmokeTests
{
    // A 1x1 transparent PNG, so the vision test needs no external asset.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private static OpenAIProvider? CreateLiveProvider(HttpClient http)
    {
        var apiKey = Environment.GetEnvironmentVariable("AIMON_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return null; // Gated: no key in the environment (e.g. CI), so skip the live call.

        return new OpenAIProvider(http, new InMemorySecretStore(("openai", apiKey)), OpenAIProviderOptions.Default());
    }

    [Fact]
    public async Task Live_Image_Mode_Returns_Image_Bytes()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var provider = CreateLiveProvider(http);
        if (provider is null)
            return;

        var request = new ProviderRequest
        {
            Model = new ModelRef("openai", "gpt-image-1"),
            Messages = [Message.User("A simple red circle on a white background.")],
            Mode = TurnMode.Image,
        };

        var items = await AsyncStream.ToListAsync(provider.StreamAsync(request, CancellationToken.None));

        var image = Assert.Single(items.OfType<ProviderStreamItem.ImageArtifact>());
        Assert.NotEmpty(image.Data);
    }

    [Fact]
    public async Task Live_Vision_Describes_An_Attached_Image()
    {
        using var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var provider = CreateLiveProvider(http);
        if (provider is null)
            return;

        var model = Environment.GetEnvironmentVariable("AIMON_OPENAI_MODEL") ?? "gpt-5.4-mini";
        var attachment = new ImageAttachment(Convert.FromBase64String(OnePixelPngBase64), "image/png");

        var request = new ProviderRequest
        {
            Model = new ModelRef("openai", model),
            Messages = [Message.User("Reply with the single word: yes, if you can see an image.", [attachment])],
            Mode = TurnMode.Chat,
        };

        var items = await AsyncStream.ToListAsync(provider.StreamAsync(request, CancellationToken.None));
        var text = string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(delta => delta.Text));

        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
