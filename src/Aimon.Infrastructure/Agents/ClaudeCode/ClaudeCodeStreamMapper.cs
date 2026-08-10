using System.Text.Json;
using Aimon.Domain.Pipeline;

namespace Aimon.Infrastructure.Agents.ClaudeCode;

/// <summary>
/// Maps Claude Code's <c>--output-format stream-json</c> events to <see cref="ProviderStreamItem"/>s:
/// the <c>session_id</c> (emitted once as a <c>session</c> event so Aimon can resume), assistant text
/// blocks as <see cref="ProviderStreamItem.TextDelta"/>, <c>tool_use</c> blocks as <c>tool</c> (and
/// <c>file-edit</c> for editing tools), and the final <c>result</c> as a <c>usage</c> summary. One
/// instance per turn — it tracks whether the session has been emitted. Never throws: an unparseable
/// line maps to nothing (implementation_plan.md, Phase 9).
/// </summary>
internal sealed class ClaudeCodeStreamMapper : ICliStreamMapper
{
    private static readonly HashSet<string> EditToolNames =
        new(StringComparer.Ordinal) { "Edit", "Write", "MultiEdit", "NotebookEdit" };

    private bool _sessionEmitted;

    public IEnumerable<ProviderStreamItem> Map(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return [];

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [];

            var items = new List<ProviderStreamItem>();

            if (!_sessionEmitted
                && root.TryGetProperty("session_id", out var sessionElement)
                && sessionElement.ValueKind == JsonValueKind.String
                && sessionElement.GetString() is { Length: > 0 } sessionId)
            {
                _sessionEmitted = true;
                items.Add(new ProviderStreamItem.ProviderEvent("session", sessionId));
            }

            var type = StringProperty(root, "type");
            switch (type)
            {
                case "assistant":
                    MapAssistant(root, items);
                    break;
                case "result":
                    MapResult(root, items);
                    break;
            }

            return items;
        }
    }

    private static void MapAssistant(JsonElement root, List<ProviderStreamItem> items)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            return;
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return;

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object)
                continue;

            switch (StringProperty(block, "type"))
            {
                case "text":
                    if (StringProperty(block, "text") is { Length: > 0 } text)
                        items.Add(new ProviderStreamItem.TextDelta(text));
                    break;
                case "tool_use":
                    MapToolUse(block, items);
                    break;
            }
        }
    }

    private static void MapToolUse(JsonElement block, List<ProviderStreamItem> items)
    {
        var name = StringProperty(block, "name");
        if (string.IsNullOrEmpty(name))
            return;

        items.Add(new ProviderStreamItem.ProviderEvent("tool", name));

        if (EditToolNames.Contains(name)
            && block.TryGetProperty("input", out var input)
            && input.ValueKind == JsonValueKind.Object
            && StringProperty(input, "file_path") is { Length: > 0 } path)
        {
            items.Add(new ProviderStreamItem.ProviderEvent("file-edit", path));
        }
    }

    private static void MapResult(JsonElement root, List<ProviderStreamItem> items)
    {
        // A non-success result (e.g. hit the turn limit) may have no assistant text — surface it.
        var subtype = StringProperty(root, "subtype");
        if (!string.Equals(subtype, "success", StringComparison.Ordinal)
            && StringProperty(root, "result") is { Length: > 0 } resultText)
        {
            items.Add(new ProviderStreamItem.TextDelta(resultText));
        }

        string? tokens = null;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            var input = NumberText(usage, "input_tokens");
            var output = NumberText(usage, "output_tokens");
            if (input is not null || output is not null)
                tokens = $"tokens: {input ?? "?"} in / {output ?? "?"} out";
        }

        var cost = NumberText(root, "total_cost_usd");
        var summary = (tokens, cost) switch
        {
            (not null, not null) => $"{tokens}, cost: ${cost}",
            (not null, null) => tokens,
            (null, not null) => $"cost: ${cost}",
            _ => null,
        };

        if (summary is not null)
            items.Add(new ProviderStreamItem.ProviderEvent("usage", summary));
    }

    private static string? StringProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? NumberText(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetRawText()
            : null;
}
