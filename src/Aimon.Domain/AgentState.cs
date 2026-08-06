namespace Aimon.Domain;

/// <summary>
/// Semantic lifecycle state of the AI during a conversation. Drives which set of
/// frames/animations the Graphics Engine loads and plays (see implementation_plan.md, §3.4).
/// The set is intentionally small and extensible.
/// </summary>
public enum AgentState
{
    /// <summary>No active request; the agent is at rest and awaiting input.</summary>
    Idle,

    /// <summary>A user message is being composed or a request is being prepared.</summary>
    Listening,

    /// <summary>A request is in flight and/or a response is being generated.</summary>
    Working,

    /// <summary>The last request failed or was blocked by hard settings.</summary>
    Error,
}
