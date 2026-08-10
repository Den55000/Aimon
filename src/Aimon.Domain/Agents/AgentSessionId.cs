namespace Aimon.Domain.Agents;

/// <summary>
/// An opaque handle to a CLI agent's own conversation session, minted by the agent (e.g. Claude
/// Code's <c>session_id</c>). Aimon persists it on the conversation and replays it on the next turn
/// (<c>--resume</c>) so the agent's own memory carries multi-turn context.
/// </summary>
public readonly record struct AgentSessionId
{
    public string Value { get; }

    private AgentSessionId(string value) => Value = value;

    public static AgentSessionId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Session id must not be blank.", nameof(value));

        return new AgentSessionId(value.Trim());
    }

    public override string ToString() => Value;
}
