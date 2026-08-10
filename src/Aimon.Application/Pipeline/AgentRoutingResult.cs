using Aimon.Domain.Agents;

namespace Aimon.Application.Pipeline;

/// <summary>
/// The outcome of routing a CLI-agent turn: either a ready-to-run <see cref="AgentTurnRequest"/> or a
/// block imposed by hard settings. The private constructor keeps the set of outcomes closed.
/// </summary>
public abstract record AgentRoutingResult
{
    private AgentRoutingResult() { }

    /// <summary>The turn is allowed; carries the request to hand to the agent backend.</summary>
    public sealed record Planned(AgentTurnRequest Request) : AgentRoutingResult;

    /// <summary>The turn was blocked locally (e.g. by hard settings); carries a human-readable reason.</summary>
    public sealed record Blocked(string Reason) : AgentRoutingResult;
}
