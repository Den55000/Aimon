using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.OpenAI;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>
/// Live smoke test against the real OpenAI API. Gated on the <c>AIMON_OPENAI_API_KEY</c> environment
/// variable, so it is a no-op in CI and by default; set the key (and optionally
/// <c>AIMON_OPENAI_MODEL</c>) to exercise it.
/// </summary>
[Trait("Category", "Live")]
public class OpenAILiveSmokeTests
{
    [Fact]
    public async Task Live_Streaming_Chat_Returns_Text_When_A_Key_Is_Provided()
    {
        var apiKey = Environment.GetEnvironmentVariable("AIMON_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return; // Gated: no key in the environment (e.g. CI), so skip the live call.

        var model = Environment.GetEnvironmentVariable("AIMON_OPENAI_MODEL") ?? "gpt-5.4-mini";

        using var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var provider = new OpenAIProvider(http, new InMemorySecretStore(("openai", apiKey)), OpenAIProviderOptions.Default());

        var request = new ProviderRequest
        {
            Model = new ModelRef("openai", model),
            Messages =
            [
                Message.System("You are a test bot. Reply with a short greeting."),
                Message.User("Say hello in three words."),
            ],
        };

        var items = await AsyncStream.ToListAsync(provider.StreamAsync(request, CancellationToken.None));
        var text = string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(delta => delta.Text));

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains(items, item => item is ProviderStreamItem.StateChange { State: AgentState.Idle });
    }
}
