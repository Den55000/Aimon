using Aimon.Application.Abstractions;
using Aimon.Application.Profiles;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Tests.TestSupport;

/// <summary>A minimal in-memory <see cref="IProfileStore"/> for exercising the authoring flow.</summary>
internal sealed class InMemoryProfileStore : IProfileStore
{
    private readonly Dictionary<ProfileId, Profile> _profiles = [];

    public IReadOnlyDictionary<ProfileId, Profile> Profiles => _profiles;

    public Task<Profile?> GetAsync(ProfileId id, CancellationToken cancellationToken)
        => Task.FromResult(_profiles.GetValueOrDefault(id));

    public Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProfileSummary> summaries =
            [.. _profiles.Values.Select(profile => new ProfileSummary
            {
                Id = profile.Id,
                Name = profile.Name,
                UpdatedAt = profile.UpdatedAt,
            })];

        return Task.FromResult(summaries);
    }

    public Task SaveAsync(Profile profile, CancellationToken cancellationToken)
    {
        _profiles[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ProfileId id, CancellationToken cancellationToken)
    {
        _profiles.Remove(id);
        return Task.CompletedTask;
    }

    public Task<Profile> ImportAsync(string bundlePath, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task ExportAsync(ProfileId id, string bundlePath, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
