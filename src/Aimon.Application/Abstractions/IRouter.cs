using Aimon.Application.Pipeline;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Builds the execution context and plan from the active profile, conversation state, hard settings,
/// and the user's message — and enforces hard settings before dispatch (Router;
/// implementation_plan.md, Phase 3).
/// </summary>
public interface IRouter
{
    RoutingResult Route(RoutingContext context);
}
