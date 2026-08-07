using Aimon.Domain.Profiles;

namespace Aimon.Application.Profiles;

/// <summary>Lightweight profile listing entry used by profile-management UI.</summary>
public sealed record ProfileSummary
{
    public required ProfileId Id { get; init; }

    public required string Name { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
