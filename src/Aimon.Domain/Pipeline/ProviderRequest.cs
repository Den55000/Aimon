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
}
