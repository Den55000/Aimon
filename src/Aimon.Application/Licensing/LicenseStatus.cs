namespace Aimon.Application.Licensing;

/// <summary>The user's current tier and whether their licence is active.</summary>
public sealed record LicenseStatus
{
    public required SubscriptionTier Tier { get; init; }

    public bool IsActive { get; init; }
}
