using System.Text;
using Aimon.Domain.Conversations;
using Aimon.Domain.Profiles;

namespace Aimon.Application.Pipeline;

/// <summary>Composes the system prompt for a turn from the structured profile, task style, and state.</summary>
internal static class SystemPromptBuilder
{
    public static string Build(StructuredProfile profile, TaskStyle? taskStyle, ConversationState conversationState)
    {
        var builder = new StringBuilder();

        var personality = profile.Personality;
        if (!string.IsNullOrWhiteSpace(personality.Persona))
            builder.AppendLine($"You are {personality.Persona}.");
        if (!string.IsNullOrWhiteSpace(personality.Tone))
            builder.AppendLine($"Tone: {personality.Tone}.");
        if (personality.Traits.Count > 0)
            builder.AppendLine($"Traits: {string.Join(", ", personality.Traits)}.");

        var rules = profile.InteractionRules;
        if (rules.Dos.Count > 0)
            builder.AppendLine($"Do: {string.Join("; ", rules.Dos)}.");
        if (rules.Donts.Count > 0)
            builder.AppendLine($"Don't: {string.Join("; ", rules.Donts)}.");
        builder.AppendLine($"Preferred verbosity: {rules.Verbosity}.");
        if (!string.IsNullOrWhiteSpace(rules.FormattingPreference))
            builder.AppendLine($"Formatting: {rules.FormattingPreference}.");

        if (taskStyle is not null && !string.IsNullOrWhiteSpace(taskStyle.SystemPromptAddition))
            builder.AppendLine(taskStyle.SystemPromptAddition);

        if (!string.IsNullOrWhiteSpace(conversationState.Summary))
            builder.AppendLine($"Conversation so far: {conversationState.Summary}");
        if (conversationState.SalientFacts.Count > 0)
            builder.AppendLine($"Relevant facts: {string.Join("; ", conversationState.SalientFacts)}.");

        return builder.ToString().TrimEnd();
    }
}
