namespace Aimon.Domain.Agents;

/// <summary>
/// Stable identifier for a chat backend — either the built-in model provider (<c>openai</c>) or an
/// external agentic CLI (<c>claude-code</c>, later <c>codex</c>, <c>cursor</c>, <c>copilot</c>).
/// Normalized to a lower-case slug so lookups are case-insensitive.
/// </summary>
public readonly record struct BackendId
{
    public string Value { get; }

    private BackendId(string value) => Value = value;

    public static BackendId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Backend id must not be blank.", nameof(value));

        return new BackendId(value.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value;
}
