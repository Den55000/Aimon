namespace Aimon.Domain.Profiles;

/// <summary>Behavioural do's/don'ts and formatting preferences that shape how the AI responds.</summary>
public sealed record InteractionRules
{
    public IReadOnlyList<string> Dos { get; init; } = [];

    public IReadOnlyList<string> Donts { get; init; } = [];

    public Verbosity Verbosity { get; init; } = Verbosity.Normal;

    public string? FormattingPreference { get; init; }

    /// <summary>Empty rule set with normal verbosity.</summary>
    public static InteractionRules Default { get; } = new();
}
