namespace Aimon.Domain.Conversations;

/// <summary>
/// One completed round-trip: the user's message, the assistant's reply, and the semantic state the
/// turn ended in. Fed to the Conversation State Reducer to update the lightweight running state.
/// </summary>
public sealed record Exchange
{
    public required Message UserMessage { get; init; }

    public required Message AssistantMessage { get; init; }

    public AgentState FinalState { get; init; } = AgentState.Idle;
}
