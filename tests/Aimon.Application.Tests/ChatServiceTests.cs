using Aimon.Application.Abstractions;
using Aimon.Application.Pipeline;
using Aimon.Application.Tests.TestSupport;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Pipeline;
using Aimon.Domain.Safety;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class ChatServiceTests
{
    private static (IChatService Chat, FakeAiProvider Provider) Build(params ProviderStreamItem[] scripted)
    {
        var provider = new FakeAiProvider(scripted);
        var services = new ServiceCollection()
            .AddAimonApplication()
            .AddSingleton<IAiProvider>(provider)
            .BuildServiceProvider();

        return (services.GetRequiredService<IChatService>(), provider);
    }

    [Fact]
    public async Task Passes_Through_The_Provider_Stream_On_The_Happy_Path()
    {
        var (chat, provider) = Build(
            new ProviderStreamItem.StateChange(AgentState.Working),
            new ProviderStreamItem.TextDelta("Hi"),
            new ProviderStreamItem.ProviderEvent("finish", "stop"),
            new ProviderStreamItem.StateChange(AgentState.Idle));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o"),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("hello"),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        Assert.Equal(1, provider.CallCount);
        Assert.Equal("Hi", string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(delta => delta.Text)));
        Assert.Contains(items, item => item is ProviderStreamItem.StateChange { State: AgentState.Idle });
    }

    [Fact]
    public async Task Blocks_Locally_Without_Calling_The_Provider()
    {
        var (chat, provider) = Build(new ProviderStreamItem.TextDelta("should not appear"));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o", new HardSettings(blockedTopics: ["politics"])),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("tell me about politics"),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        Assert.Equal(0, provider.CallCount);
        Assert.Contains(items, item => item is ProviderStreamItem.StateChange { State: AgentState.Error });
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "blocked" });
    }
}
