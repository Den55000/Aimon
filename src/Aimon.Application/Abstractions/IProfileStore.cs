using Aimon.Application.Profiles;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Abstractions;

/// <summary>
/// Persists profiles and supports export/import as portable bundles (implementation_plan.md, Phase 2).
/// </summary>
public interface IProfileStore
{
    Task<Profile?> GetAsync(ProfileId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(Profile profile, CancellationToken cancellationToken);

    Task DeleteAsync(ProfileId id, CancellationToken cancellationToken);

    Task<Profile> ImportAsync(string bundlePath, CancellationToken cancellationToken);

    Task ExportAsync(ProfileId id, string bundlePath, CancellationToken cancellationToken);
}
