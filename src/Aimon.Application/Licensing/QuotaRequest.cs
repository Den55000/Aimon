namespace Aimon.Application.Licensing;

/// <summary>A request to check whether a given model call is permitted under the current tier.</summary>
public sealed record QuotaRequest
{
    public required SubscriptionTier Tier { get; init; }

    public required string Model { get; init; }

    /// <summary>Whether the user is supplying their own API key (may bypass metering per tier).</summary>
    public bool UsesOwnApiKey { get; init; }
}
