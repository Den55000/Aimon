using Aimon.Domain.Agents;
using Aimon.Domain.Conversations;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Pipeline;

/// <summary>Everything the Router needs to plan a single CLI-agent turn.</summary>
public sealed record AgentRoutingContext
{
    public required Profile Profile { get; init; }

    public required Message UserMessage { get; init; }

    /// <summary>The working directory the agent runs in.</summary>
    public required WorkspaceContext Workspace { get; init; }

    /// <summary>The agent's session to resume, if this conversation already has one.</summary>
    public AgentSessionId? SessionId { get; init; }

    public AgentPermissionMode PermissionMode { get; init; } = AgentPermissionMode.Full;

    /// <summary>Optional task hint used to select a <c>TaskStyle</c> override.</summary>
    public string? TaskName { get; init; }
}
