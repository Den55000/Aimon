using Aimon.Domain.Conversations;

namespace Aimon.Domain.Agents;

/// <summary>
/// A ready-to-run request for a CLI agent backend — the agent analogue of
/// <see cref="Pipeline.ProviderRequest"/>. Produced by the Router (which enforces hard settings and
/// builds the persona system prompt) and handed to an <c>IAgentBackend</c>.
/// </summary>
public sealed record AgentTurnRequest
{
    /// <summary>The user's prompt for this turn.</summary>
    public required string Prompt { get; init; }

    /// <summary>The working directory the agent runs in and may modify.</summary>
    public required WorkspaceContext Workspace { get; init; }

    /// <summary>Optional persona/system prompt, appended to the agent's own system prompt.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>How much autonomy the agent has over the workspace this turn.</summary>
    public AgentPermissionMode PermissionMode { get; init; } = AgentPermissionMode.Full;

    /// <summary>The agent's own session to resume, if this is a continuing conversation.</summary>
    public AgentSessionId? SessionId { get; init; }

    /// <summary>Optional CLI-specific model name (e.g. <c>sonnet</c>); null lets the CLI use its default.</summary>
    public string? Model { get; init; }

    /// <summary>Optional image inputs, for agents that accept them.</summary>
    public IReadOnlyList<ImageAttachment> Attachments { get; init; } = [];
}
