using Aimon.Application.Agents;
using Aimon.Domain.Agents;

namespace Aimon.Application.Abstractions;

/// <summary>
/// The catalog of chat backends — the built-in model provider plus every registered CLI agent. The
/// UI lists <see cref="Descriptors"/> in the backend picker; the orchestrator resolves the selected
/// backend to dispatch a turn. Adding a new CLI agent is a matter of registering another
/// <see cref="IAgentBackend"/> (implementation_plan.md, Phase 9).
/// </summary>
public interface IChatBackendRegistry
{
    /// <summary>All backends (model provider first, then CLI agents), for the picker.</summary>
    IReadOnlyList<BackendDescriptor> Descriptors { get; }

    /// <summary>The default backend used when a turn doesn't name one (the model provider).</summary>
    BackendId DefaultBackendId { get; }

    /// <summary>The descriptor for a backend, or null if unknown.</summary>
    BackendDescriptor? Find(BackendId id);

    /// <summary>Resolves a CLI agent backend by id; false for the model provider or an unknown id.</summary>
    bool TryGetAgent(BackendId id, out IAgentBackend backend);
}
