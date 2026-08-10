using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.Agents.ClaudeCode;

namespace Aimon.Infrastructure.Tests;

/// <summary>
/// Contract tests for the Claude Code <c>stream-json</c> mapper against recorded event lines — no real
/// process involved. If the CLI's event schema drifts, these fixtures are where it's pinned.
/// </summary>
public class ClaudeCodeStreamMapperTests
{
    private static readonly string[] Transcript =
    [
        """{"type":"system","subtype":"init","session_id":"sess-abc","tools":["Read","Edit"],"cwd":"/repo"}""",
        """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"I'll fix that."}]},"session_id":"sess-abc"}""",
        """{"type":"assistant","message":{"role":"assistant","content":[{"type":"tool_use","name":"Edit","input":{"file_path":"/repo/app.cs"}}]},"session_id":"sess-abc"}""",
        """{"type":"result","subtype":"success","result":"Done.","session_id":"sess-abc","usage":{"input_tokens":10,"output_tokens":5},"total_cost_usd":0.001}""",
    ];

    private static List<ProviderStreamItem> MapAll(IEnumerable<string> lines)
    {
        var mapper = new ClaudeCodeStreamMapper();
        return lines.SelectMany(mapper.Map).ToList();
    }

    [Fact]
    public void Emits_The_Session_Id_Once()
    {
        var items = MapAll(Transcript);

        var session = Assert.Single(items.OfType<ProviderStreamItem.ProviderEvent>(), e => e.Kind == "session");
        Assert.Equal("sess-abc", (string?)session.Payload);
    }

    [Fact]
    public void Maps_Assistant_Text_To_Deltas()
    {
        var items = MapAll(Transcript);

        var text = string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(delta => delta.Text));

        // The success result text ("Done.") is NOT re-emitted — assistant events already carried it.
        Assert.Equal("I'll fix that.", text);
    }

    [Fact]
    public void Maps_Tool_Use_To_A_Tool_And_File_Edit_Event()
    {
        var items = MapAll(Transcript);

        Assert.Contains(items, i => i is ProviderStreamItem.ProviderEvent { Kind: "tool", Payload: "Edit" });
        Assert.Contains(items, i => i is ProviderStreamItem.ProviderEvent { Kind: "file-edit", Payload: "/repo/app.cs" });
    }

    [Fact]
    public void Maps_The_Result_To_A_Usage_Summary()
    {
        var items = MapAll(Transcript);

        var usage = Assert.Single(items.OfType<ProviderStreamItem.ProviderEvent>(), e => e.Kind == "usage");
        Assert.Contains("tokens", (string)usage.Payload!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Non_Success_Result_Surfaces_Its_Text()
    {
        var line = """{"type":"result","subtype":"error_max_turns","result":"Hit the turn limit.","session_id":"s1"}""";

        var items = MapAll([line]);

        Assert.Contains(items, i => i is ProviderStreamItem.TextDelta { Text: "Hit the turn limit." });
    }

    [Fact]
    public void An_Unparseable_Line_Maps_To_Nothing()
    {
        var mapper = new ClaudeCodeStreamMapper();

        Assert.Empty(mapper.Map("not json"));
        Assert.Empty(mapper.Map(""));
        Assert.Empty(mapper.Map("[1,2,3]"));
    }
}
