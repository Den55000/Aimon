namespace Aimon.Domain.Profiles;

/// <summary>
/// The compiled, structured form of a profile: the fields the pipeline actually reads. Produced from
/// the natural-language source by the Profile Compiler (implementation_plan.md, Phase 5).
/// </summary>
public sealed record StructuredProfile
{
    public Personality Personality { get; init; } = Personality.Neutral;

    public InteractionRules InteractionRules { get; init; } = InteractionRules.Default;

    public ModelPreferences ModelPreferences { get; init; } = ModelPreferences.Empty;

    public IReadOnlyList<TaskStyle> TaskStyles { get; init; } = [];

    /// <summary>A structured profile with neutral personality, default rules, and no overrides.</summary>
    public static StructuredProfile Empty { get; } = new();
}
