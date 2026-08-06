using Aimon.Application.Abstractions;
using Aimon.Domain;

namespace Aimon.Infrastructure.Greeting;

/// <summary>
/// Phase 0 placeholder implementation of <see cref="IGreetingService"/>. It touches the Domain
/// (<see cref="AgentState"/>) so the full dependency chain is exercised at runtime.
/// </summary>
internal sealed class GreetingService : IGreetingService
{
    public string GetGreeting()
        => $"Aimon is ready. Current agent state: {AgentState.Idle}.";
}
