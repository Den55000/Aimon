using Aimon.Application.Licensing;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Client-side licensing seam, backed by a mock backend in this plan (implementation_plan.md, Phase 8).
/// </summary>
public interface ILicenseService
{
    Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken);
}
