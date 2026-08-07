using Aimon.Application.Abstractions;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Microsoft.Extensions.DependencyInjection;

namespace Aimon.Application.Tests;

public class ConversationStateReducerTests
{
    private static IConversationStateReducer CreateReducer()
        => new ServiceCollection().AddAimonApplication().BuildServiceProvider().GetRequiredService<IConversationStateReducer>();

    private static Exchange Turn(string userText, string assistantText = "ok", AgentState finalState = AgentState.Idle) => new()
    {
        UserMessage = Message.User(userText),
        AssistantMessage = Message.Assistant(assistantText),
        FinalState = finalState,
    };

    [Fact]
    public void Increments_The_Turn_Count()
    {
        var reducer = CreateReducer();

        var afterOne = reducer.Reduce(ConversationState.Empty, Turn("hello"));
        var afterTwo = reducer.Reduce(afterOne, Turn("again"));

        Assert.Equal(1, afterOne.TurnCount);
        Assert.Equal(2, afterTwo.TurnCount);
    }

    [Fact]
    public void Captures_First_Person_Statements_As_Salient_Facts()
    {
        var state = CreateReducer().Reduce(ConversationState.Empty, Turn("My name is Denys. I prefer concise answers."));

        Assert.Contains(state.SalientFacts, fact => fact.Contains("name is Denys", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(state.SalientFacts, fact => fact.Contains("prefer concise", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ignores_Transient_Questions_That_State_No_Facts()
    {
        var state = CreateReducer().Reduce(ConversationState.Empty, Turn("What is the capital of France?"));

        Assert.Empty(state.SalientFacts);
    }

    [Fact]
    public void Retains_Earlier_Facts_Across_Many_Later_Turns()
    {
        var reducer = CreateReducer();

        var state = reducer.Reduce(ConversationState.Empty, Turn("My name is Denys."));

        // Five transient, fact-free turns in between must not evict the established fact.
        for (var i = 0; i < 5; i++)
            state = reducer.Reduce(state, Turn($"What is fact number {i}?"));

        Assert.Contains(state.SalientFacts, fact => fact.Contains("name is Denys", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(6, state.TurnCount);
    }

    [Fact]
    public void Does_Not_Duplicate_An_Identically_Repeated_Fact()
    {
        var reducer = CreateReducer();

        var state = reducer.Reduce(ConversationState.Empty, Turn("I use TypeScript."));
        state = reducer.Reduce(state, Turn("What is a generic?"));
        state = reducer.Reduce(state, Turn("I use TypeScript."));

        Assert.Single(state.SalientFacts, fact => fact.Contains("use TypeScript", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, state.TurnCount);
    }

    [Fact]
    public void Keeps_State_Bounded_Over_A_Long_Conversation()
    {
        var reducer = CreateReducer();
        var state = ConversationState.Empty;

        // 50 distinct fact-bearing turns — far more than the reducer retains.
        for (var i = 0; i < 50; i++)
            state = reducer.Reduce(state, Turn($"My lucky number is {i}."));

        // Bounded fact list, and the most recent fact survives while the oldest is evicted.
        Assert.True(state.SalientFacts.Count is > 0 and <= 12, $"fact count was {state.SalientFacts.Count}");
        Assert.Contains(state.SalientFacts, fact => fact.Contains("number is 49", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(state.SalientFacts, fact => fact.Contains("number is 0.", StringComparison.OrdinalIgnoreCase));

        // Bounded summary regardless of how many turns contributed.
        Assert.NotNull(state.Summary);
        Assert.True(state.Summary!.Length <= 320, $"summary length was {state.Summary.Length}");
        Assert.Equal(50, state.TurnCount);
    }
}
