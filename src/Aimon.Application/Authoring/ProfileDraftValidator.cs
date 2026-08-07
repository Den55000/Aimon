using Aimon.Domain.Profiles;

namespace Aimon.Application.Authoring;

/// <summary>
/// Lightweight schema/sanity validation of a compiled <see cref="StructuredProfile"/> draft before it
/// can be confirmed (implementation_plan.md, Phase 5). It only flags genuinely unusable output, so a
/// normal draft always passes; the deeper value-object invariants are already enforced when the
/// compiler adapter reconstructs the Domain types.
/// </summary>
internal static class ProfileDraftValidator
{
    public static IReadOnlyList<string> Validate(StructuredProfile draft)
    {
        var issues = new List<string>();

        var personality = draft.Personality;
        var rules = draft.InteractionRules;

        var hasPersonality =
            !string.IsNullOrWhiteSpace(personality.Persona) ||
            !string.IsNullOrWhiteSpace(personality.Tone) ||
            personality.Traits.Count > 0;

        var hasRules =
            rules.Dos.Count > 0 ||
            rules.Donts.Count > 0 ||
            !string.IsNullOrWhiteSpace(rules.FormattingPreference);

        if (!hasPersonality && !hasRules)
            issues.Add("The draft has no personality or interaction rules — describe the persona in more detail and recompile.");

        for (var i = 0; i < draft.TaskStyles.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(draft.TaskStyles[i].TaskName))
                issues.Add($"Task style #{i + 1} has no task name.");
        }

        return issues;
    }
}
