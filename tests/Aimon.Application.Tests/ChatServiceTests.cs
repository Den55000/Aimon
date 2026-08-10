using Aimon.Application.Abstractions;
using Aimon.Application.Pipeline;
using Aimon.Application.Tests.TestSupport;
using Aimon.Domain;
using Aimon.Domain.Agents;
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

    private static (IChatService Chat, FakeAgentBackend Agent) BuildWithAgent(params ProviderStreamItem[] scripted)
    {
        var agent = new FakeAgentBackend("claude-code", scripted);
        var services = new ServiceCollection()
            .AddAimonApplication()
            .AddSingleton<IAiProvider>(new FakeAiProvider())
            .AddSingleton<IAgentBackend>(agent)
            .BuildServiceProvider();

        return (services.GetRequiredService<IChatService>(), agent);
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

        // A blocked turn leaves the running state untouched — no reduced-state event is emitted.
        Assert.DoesNotContain(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "conversation" });
    }

    [Fact]
    public async Task Emits_The_Reduced_Conversation_State_After_A_Completed_Turn()
    {
        var (chat, _) = Build(
            new ProviderStreamItem.StateChange(AgentState.Working),
            new ProviderStreamItem.TextDelta("Nice to meet you."),
            new ProviderStreamItem.StateChange(AgentState.Idle));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o"),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("My name is Denys."),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        var conversationEvent = Assert.Single(
            items.OfType<ProviderStreamItem.ProviderEvent>(), e => e.Kind == "conversation");
        var reduced = Assert.IsType<ConversationState>(conversationEvent.Payload);
        Assert.Equal(1, reduced.TurnCount);
        Assert.Contains(reduced.SalientFacts, fact => fact.Contains("name is Denys", StringComparison.OrdinalIgnoreCase));
    }

    // --- Agent backend path -----------------------------------------------------------------

    [Fact]
    public async Task Routes_To_The_Selected_Agent_Backend_And_Skips_The_Reducer()
    {
        var (chat, agent) = BuildWithAgent(
            new ProviderStreamItem.StateChange(AgentState.Working),
            new ProviderStreamItem.TextDelta("Edited file."),
            new ProviderStreamItem.ProviderEvent("session", "sess-9"),
            new ProviderStreamItem.StateChange(AgentState.Idle));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o"),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("fix the bug"),
            Backend = BackendId.From("claude-code"),
            Workspace = new WorkspaceContext("/repo"),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        Assert.Equal(1, agent.CallCount);
        Assert.Equal("fix the bug", agent.LastRequest!.Prompt);
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "session" });

        // The agent's own session carries memory, so Aimon's reducer never runs on this path.
        Assert.DoesNotContain(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "conversation" });
    }

    [Fact]
    public async Task Agent_Turn_Without_A_Workspace_Errors_Before_Launch()
    {
        var (chat, agent) = BuildWithAgent(new ProviderStreamItem.TextDelta("should not appear"));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o"),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("do something"),
            Backend = BackendId.From("claude-code"),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        Assert.Equal(0, agent.CallCount);
        Assert.Contains(items, item => item is ProviderStreamItem.StateChange { State: AgentState.Error });
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "error" });
    }

    [Fact]
    public async Task An_Unknown_Backend_Errors()
    {
        var (chat, agent) = BuildWithAgent(new ProviderStreamItem.TextDelta("should not appear"));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o"),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("hi"),
            Backend = BackendId.From("does-not-exist"),
            Workspace = new WorkspaceContext("/repo"),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        Assert.Equal(0, agent.CallCount);
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "error" });
    }

    [Fact]
    public async Task Agent_Turn_Blocked_By_Hard_Settings_Does_Not_Launch()
    {
        var (chat, agent) = BuildWithAgent(new ProviderStreamItem.TextDelta("should not appear"));

        var request = new SendMessageRequest
        {
            Profile = TestProfiles.WithModel("gpt-4o", new HardSettings(blockedTopics: ["secrets"])),
            ConversationState = ConversationState.Empty,
            UserMessage = Message.User("leak the secrets"),
            Backend = BackendId.From("claude-code"),
            Workspace = new WorkspaceContext("/repo"),
        };

        var items = await AsyncStream.ToListAsync(chat.SendAsync(request, CancellationToken.None));

        Assert.Equal(0, agent.CallCount);
        Assert.Contains(items, item => item is ProviderStreamItem.ProviderEvent { Kind: "blocked" });
    }
}
