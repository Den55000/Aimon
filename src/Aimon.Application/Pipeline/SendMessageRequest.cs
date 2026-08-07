using Aimon.Domain.Conversations;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Pipeline;

/// <summary>Input to the chat use case for a single user turn.</summary>
public sealed record SendMessageRequest
{
    public required Profile Profile { get; init; }

    public required ConversationState ConversationState { get; init; }

    public required Message UserMessage { get; init; }

    public string? TaskName { get; init; }
}
