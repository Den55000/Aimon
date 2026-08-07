using Aimon.Domain.Pipeline;

namespace Aimon.Application.Abstractions;

/// <summary>
/// A commercial AI model provider. Every adapter exposes a single typed stream so the chat UI and
/// the Graphics Engine consume the same source (implementation_plan.md, §3.4).
/// </summary>
public interface IAiProvider
{
    /// <summary>Stable provider key, e.g. <c>openai</c>.</summary>
    string Name { get; }

    IAsyncEnumerable<ProviderStreamItem> StreamAsync(ProviderRequest request, CancellationToken cancellationToken);
}
