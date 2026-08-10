using Aimon.Domain.Agents;

namespace Aimon.Application.Agents;

/// <summary>
/// Static capability metadata for a chat backend, used to populate the UI picker and to decide how a
/// turn is dispatched. Runtime availability (is the CLI installed / logged in?) is probed separately
/// via <see cref="Abstractions.IAgentBackend.CheckAvailabilityAsync"/>.
/// </summary>
public sealed record BackendDescriptor
{
    public required BackendId Id { get; init; }

    /// <summary>Human-readable name for the picker, e.g. <c>Claude Code</c>.</summary>
    public required string DisplayName { get; init; }

    public required BackendKind Kind { get; init; }

    /// <summary>True if the backend operates on a working directory (all CLI agents).</summary>
    public bool SupportsWorkspace { get; init; }

    /// <summary>True if the backend carries multi-turn memory via its own session (resume).</summary>
    public bool SupportsSessions { get; init; }

    /// <summary>True if the backend can produce or accept images.</summary>
    public bool SupportsImages { get; init; }

    public BackendAuthKind AuthKind { get; init; }
}
