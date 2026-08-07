using Aimon.Domain.Conversations;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Pipeline;

/// <summary>Everything the Router needs to plan a single turn.</summary>
public sealed record RoutingContext
{
    public required Profile Profile { get; init; }

    public required ConversationState ConversationState { get; init; }

    public required Message UserMessage { get; init; }

    /// <summary>Optional task hint used to select a <c>TaskStyle</c> override.</summary>
    public string? TaskName { get; init; }
}
