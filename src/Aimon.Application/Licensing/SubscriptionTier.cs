namespace Aimon.Application.Licensing;

/// <summary>
/// Client-side subscription tiers used for gating (implementation_plan.md, D2 / Phase 8). Backed by
/// a mock backend in this plan.
/// </summary>
public enum SubscriptionTier
{
    Free,
    Basic,
    Standard,
    Pro,
    PayAsYouGo,
}
