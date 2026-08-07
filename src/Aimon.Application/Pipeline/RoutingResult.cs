using Aimon.Domain.Pipeline;

namespace Aimon.Application.Pipeline;

/// <summary>
/// The outcome of routing a turn: either a ready-to-run <see cref="ExecutionPlan"/> or a block
/// imposed by hard settings. The private constructor keeps the set of outcomes closed.
/// </summary>
public abstract record RoutingResult
{
    private RoutingResult() { }

    /// <summary>The turn is allowed; carries the plan to execute.</summary>
    public sealed record Planned(ExecutionPlan Plan) : RoutingResult;

    /// <summary>The turn was blocked locally (e.g. by hard settings); carries a human-readable reason.</summary>
    public sealed record Blocked(string Reason) : RoutingResult;
}
