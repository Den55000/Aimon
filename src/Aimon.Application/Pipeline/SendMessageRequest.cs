using Aimon.Domain.Agents;
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

    /// <summary>Which backend to run this turn on. Null uses the default model provider.</summary>
    public BackendId? Backend { get; init; }

    /// <summary>The working directory, required when <see cref="Backend"/> is a CLI agent.</summary>
    public WorkspaceContext? Workspace { get; init; }

    /// <summary>The CLI agent session to resume, if this conversation already has one.</summary>
    public AgentSessionId? SessionId { get; init; }

    /// <summary>Autonomy granted to a CLI agent this turn. Defaults to full (Phase 9 decision A3).</summary>
    public AgentPermissionMode PermissionMode { get; init; } = AgentPermissionMode.Full;
}
