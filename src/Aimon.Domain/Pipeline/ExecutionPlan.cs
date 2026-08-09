using Aimon.Domain.Conversations;
using Aimon.Domain.Graphics;
using Aimon.Domain.Models;

namespace Aimon.Domain.Pipeline;

/// <summary>
/// The Router's output: the resolved model, the assembled context messages, generation parameters,
/// and graphics configuration for a single turn. The Interpreter turns this into a
/// <see cref="ProviderRequest"/>.
/// </summary>
public sealed record ExecutionPlan
{
    public required ModelRef Model { get; init; }

    public IReadOnlyList<Message> Messages { get; init; } = [];

    public GenerationParameters Parameters { get; init; } = GenerationParameters.Default;

    public GraphicsConfiguration Graphics { get; init; } = GraphicsConfiguration.Default;

    /// <summary>How this turn should be handled (auto / plain chat / forced image generation).</summary>
    public TurnMode Mode { get; init; } = TurnMode.Auto;

    /// <summary>When true, the chat request offers the image-generation tool (used in <see cref="TurnMode.Auto"/>).</summary>
    public bool ImageToolEnabled { get; init; }
}
