namespace Aimon.Domain.Safety;

/// <summary>
/// User-defined safety blocks enforced locally before any request is dispatched to a provider
/// (implementation_plan.md, §3.2 / Phase 3). Entries are normalized on construction: trimmed,
/// blanks dropped, and de-duplicated case-insensitively. Lookups are case-insensitive.
/// </summary>
public sealed record HardSettings
{
    public IReadOnlyList<string> BlockedTopics { get; }

    public IReadOnlyList<string> BlockedModels { get; }

    public IReadOnlyList<string> BlockedActions { get; }

    public HardSettings(
        IEnumerable<string>? blockedTopics = null,
        IEnumerable<string>? blockedModels = null,
        IEnumerable<string>? blockedActions = null)
    {
        BlockedTopics = Normalize(blockedTopics);
        BlockedModels = Normalize(blockedModels);
        BlockedActions = Normalize(blockedActions);
    }

    /// <summary>No blocks of any kind.</summary>
    public static HardSettings None { get; } = new();

    public bool IsTopicBlocked(string topic) => Contains(BlockedTopics, topic);

    public bool IsModelBlocked(string model) => Contains(BlockedModels, model);

    public bool IsActionBlocked(string action) => Contains(BlockedActions, action);

    private static bool Contains(IReadOnlyList<string> values, string candidate)
        => !string.IsNullOrWhiteSpace(candidate)
           && values.Contains(candidate.Trim(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? values)
    {
        if (values is null)
            return [];

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
