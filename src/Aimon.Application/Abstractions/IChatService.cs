using Aimon.Application.Pipeline;
using Aimon.Domain.Pipeline;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Orchestrates a single user turn end-to-end: Router → Interpreter → provider stream, surfacing the
/// same typed stream to the chat UI and the Graphics Engine. Implemented in Phase 3.
/// </summary>
public interface IChatService
{
    IAsyncEnumerable<ProviderStreamItem> SendAsync(SendMessageRequest request, CancellationToken cancellationToken);
}
