namespace Aimon.Application.Licensing;

/// <summary>The result of a quota check: either allowed, or denied with a reason.</summary>
public abstract record QuotaDecision
{
    private QuotaDecision() { }

    public sealed record Allowed : QuotaDecision;

    public sealed record Denied(string Reason) : QuotaDecision;
}
