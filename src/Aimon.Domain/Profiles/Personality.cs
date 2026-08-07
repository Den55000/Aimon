namespace Aimon.Domain.Profiles;

/// <summary>The stylistic identity of a profile: persona description, tone, and trait keywords.</summary>
public sealed record Personality
{
    public string Persona { get; init; } = "";

    public string Tone { get; init; } = "";

    public IReadOnlyList<string> Traits { get; init; } = [];

    /// <summary>A blank personality with no persona, tone, or traits.</summary>
    public static Personality Neutral { get; } = new();
}
