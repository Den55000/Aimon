namespace Aimon.Domain.Pipeline;

/// <summary>
/// One item in a provider's response stream — a closed set of variants (generated text, a semantic
/// <see cref="AgentState"/> change, or a generic provider event) consumed by both the chat UI and
/// the Graphics Engine (implementation_plan.md, §3.4). The private constructor keeps the hierarchy
/// closed to the variants declared here.
/// </summary>
public abstract record ProviderStreamItem
{
    private ProviderStreamItem() { }

    /// <summary>A chunk of generated text.</summary>
    public sealed record TextDelta(string Text) : ProviderStreamItem;

    /// <summary>A transition of the AI's semantic state.</summary>
    public sealed record StateChange(AgentState State) : ProviderStreamItem;

    /// <summary>A generated image: raw bytes, their media type, and an optional caption/prompt.</summary>
    public sealed record ImageArtifact(byte[] Data, string MediaType, string? Caption = null) : ProviderStreamItem;

    /// <summary>A provider-specific event: tool call, finish reason, usage, error, etc.</summary>
    public sealed record ProviderEvent(string Kind, object? Payload = null) : ProviderStreamItem;
}
