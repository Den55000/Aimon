using Aimon.Application.Agents;
using Aimon.Domain.Agents;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Abstractions;

/// <summary>
/// An external agentic coding CLI (Claude Code, later Codex/Cursor/Copilot) driven as a backend.
/// Adapters emit the <b>same</b> <see cref="ProviderStreamItem"/> stream as model providers, so the
/// chat UI and the Graphics Engine consume both identically (implementation_plan.md, §3.4, Phase 9).
/// </summary>
public interface IAgentBackend
{
    /// <summary>Static capabilities of this backend.</summary>
    BackendDescriptor Descriptor { get; }

    /// <summary>Probes whether the CLI can run now (installed / on PATH) and its version.</summary>
    ValueTask<BackendAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs one turn: launches the CLI in the workspace, resuming the session when present, and
    /// maps its streamed events to <see cref="ProviderStreamItem"/>s (text, <c>tool</c>/<c>file-edit</c>/
    /// <c>session</c> events, and <see cref="AgentState"/> changes).
    /// </summary>
    IAsyncEnumerable<ProviderStreamItem> StreamAsync(AgentTurnRequest request, CancellationToken cancellationToken);
}
