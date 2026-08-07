using Aimon.Application.Abstractions;
using Aimon.Domain.Conversations;

namespace Aimon.Application.Pipeline;

/// <summary>
/// Default deterministic <see cref="IConversationStateReducer"/>. It folds each completed
/// <see cref="Exchange"/> into a compact, bounded <see cref="ConversationState"/> using simple
/// heuristics — no model call — so multi-turn context stays cheap and predictable. An LLM-assisted
/// reducer can later replace this behind the same port (implementation_plan.md, Phase 4).
/// </summary>
internal sealed class ConversationStateReducer : IConversationStateReducer
{
    // Bounds that keep the running state small regardless of how long the conversation runs.
    private const int MaxFacts = 12;
    private const int MaxFactLength = 160;
    private const int MaxSummaryChars = 320;

    private const string SummarySeparator = " | ";
    private const string SummaryEllipsis = "… ";

    private static readonly char[] SentenceTerminators = ['.', '!', '?', '\n', '\r'];

    // First-person / memory markers that flag a user sentence as worth carrying forward.
    private static readonly HashSet<string> SalientTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "i", "i'm", "im", "i've", "ive", "i'll", "my", "me", "mine", "myself",
        "we", "our", "ours", "us", "remember",
    };

    public ConversationState Reduce(ConversationState previous, Exchange latest)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(latest);

        return new ConversationState
        {
            Summary = ExtendSummary(previous.Summary, latest.UserMessage.Content),
            SalientFacts = ExtractFacts(previous.SalientFacts, latest.UserMessage.Content),
            TurnCount = previous.TurnCount + 1,
        };
    }

    // Keep the most recent salient sentences: deduped, newest last, capped at MaxFacts.
    private static IReadOnlyList<string> ExtractFacts(IReadOnlyList<string> existing, string userMessage)
    {
        var facts = new List<string>(existing);

        foreach (var sentence in SplitSentences(userMessage))
        {
            if (!IsSalient(sentence))
                continue;

            var fact = Truncate(sentence, MaxFactLength);

            // Drop any prior copy so a repeated fact refreshes its recency instead of duplicating.
            facts.RemoveAll(existingFact => string.Equals(existingFact, fact, StringComparison.OrdinalIgnoreCase));
            facts.Add(fact);
        }

        if (facts.Count > MaxFacts)
            facts.RemoveRange(0, facts.Count - MaxFacts);

        return facts;
    }

    private static string? ExtendSummary(string? previousSummary, string userMessage)
    {
        var gist = Truncate(FirstSentence(userMessage), MaxFactLength);
        if (string.IsNullOrEmpty(gist))
            return previousSummary;

        var combined = string.IsNullOrEmpty(previousSummary) ? gist : previousSummary + SummarySeparator + gist;
        return ClampSummary(combined);
    }

    // Bound the summary to whole gists within MaxSummaryChars, keeping the newest.
    private static string ClampSummary(string summary)
    {
        if (summary.Length <= MaxSummaryChars)
            return summary;

        var tail = summary[^(MaxSummaryChars - SummaryEllipsis.Length)..];
        var boundary = tail.IndexOf(SummarySeparator, StringComparison.Ordinal);
        if (boundary >= 0)
            tail = tail[(boundary + SummarySeparator.Length)..];

        return SummaryEllipsis + tail;
    }

    private static IEnumerable<string> SplitSentences(string text)
        => text.Split(SentenceTerminators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FirstSentence(string text)
    {
        foreach (var sentence in SplitSentences(text))
            return sentence;

        return text.Trim();
    }

    private static bool IsSalient(string sentence)
    {
        foreach (var token in Tokenize(sentence))
        {
            if (SalientTokens.Contains(token))
                return true;
        }

        return false;
    }

    // Words are runs of letters and apostrophes, so "I'm" and "my" tokenize cleanly.
    private static IEnumerable<string> Tokenize(string sentence)
    {
        var start = -1;
        for (var i = 0; i < sentence.Length; i++)
        {
            var isWordChar = char.IsLetter(sentence[i]) || sentence[i] == '\'';
            if (isWordChar)
            {
                if (start < 0)
                    start = i;
            }
            else if (start >= 0)
            {
                yield return sentence[start..i];
                start = -1;
            }
        }

        if (start >= 0)
            yield return sentence[start..];
    }

    private static string Truncate(string value, int maxLength)
    {
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";
    }
}
