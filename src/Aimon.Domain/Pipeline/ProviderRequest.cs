using Aimon.Domain.Conversations;
using Aimon.Domain.Models;

namespace Aimon.Domain.Pipeline;

/// <summary>
/// The Interpreter's output: a provider-agnostic request ready to hand to an <c>IAiProvider</c>.
/// </summary>
public sealed record ProviderRequest
{
    public required ModelRef Model { get; init; }

    public IReadOnlyList<Message> Messages { get; init; } = [];

    public GenerationParameters Parameters { get; init; } = GenerationParameters.Default;

    public bool Stream { get; init; } = true;

    /// <summary>How this turn should be handled (auto / plain chat / forced image generation).</summary>
    public TurnMode Mode { get; init; } = TurnMode.Auto;

    /// <summary>When true, the chat request offers the image-generation tool (used in <see cref="TurnMode.Auto"/>).</summary>
    public bool ImageToolEnabled { get; init; }
}
