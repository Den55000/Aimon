namespace Aimon.Domain.Conversations;

/// <summary>
/// A compact, running summary of a conversation carried between turns and fed back into the Router
/// (implementation_plan.md, Phase 4). Kept intentionally small so it stays cheap to pass along.
/// </summary>
public sealed record ConversationState
{
    public string? Summary { get; init; }

    public IReadOnlyList<string> SalientFacts { get; init; } = [];

    public int TurnCount { get; init; }

    /// <summary>The state of a brand-new conversation.</summary>
    public static ConversationState Empty { get; } = new();
}
