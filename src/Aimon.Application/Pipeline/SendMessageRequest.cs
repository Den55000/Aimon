using Aimon.Domain.Conversations;
using Aimon.Domain.Pipeline;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Pipeline;

/// <summary>Input to the chat use case for a single user turn.</summary>
public sealed record SendMessageRequest
{
    public required Profile Profile { get; init; }

    public required ConversationState ConversationState { get; init; }

    public required Message UserMessage { get; init; }

    public string? TaskName { get; init; }

    /// <summary>How the user wants this turn handled (auto / chat / image). Defaults to auto.</summary>
    public TurnMode Mode { get; init; } = TurnMode.Auto;
}
