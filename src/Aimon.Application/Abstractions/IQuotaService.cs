using Aimon.Application.Licensing;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Decides whether a model call is permitted under the current tier and key arrangement
/// (implementation_plan.md, Phase 8).
/// </summary>
public interface IQuotaService
{
    Task<QuotaDecision> CheckAsync(QuotaRequest request, CancellationToken cancellationToken);
}
