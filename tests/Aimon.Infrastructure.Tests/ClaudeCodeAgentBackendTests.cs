using Aimon.Domain;
using Aimon.Domain.Agents;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.Agents;
using Aimon.Infrastructure.Agents.ClaudeCode;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class ClaudeCodeAgentBackendTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AgentTurnRequest Request(
        AgentPermissionMode mode = AgentPermissionMode.Full,
        string prompt = "do the thing",
        AgentSessionId? session = null,
        string? system = null,
        string? model = null) => new()
    {
        Prompt = prompt,
        Workspace = new WorkspaceContext("/repo"),
        PermissionMode = mode,
        SessionId = session,
        SystemPrompt = system,
        Model = model,
    };

    private static void AssertFlagFollowedBy(IReadOnlyList<string> args, string flag, string value)
    {
        var index = args.ToList().IndexOf(flag);
        Assert.True(index >= 0 && index + 1 < args.Count, $"expected flag '{flag}' with a value");
        Assert.Equal(value, args[index + 1]);
    }

    // --- Argument construction ---------------------------------------------------------------

    [Fact]
    public void BuildArguments_Uses_Print_StreamJson_And_BypassPermissions_In_Full_Mode()
    {
        var args = ClaudeCodeAgentBackend.BuildArguments(Request(AgentPermissionMode.Full), ClaudeCodeOptions.Default());

        Assert.Contains("-p", args);
        AssertFlagFollowedBy(args, "--output-format", "stream-json");
        Assert.Contains("--verbose", args);
        AssertFlagFollowedBy(args, "--permission-mode", "bypassPermissions");
        Assert.DoesNotContain("--resume", args);
        Assert.DoesNotContain("--append-system-prompt", args);
        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void BuildArguments_Adds_Resume_System_Prompt_And_Model_When_Present()
    {
        var request = Request(session: AgentSessionId.From("sess-1"), system: "You are helpful.", model: "sonnet");

        var args = ClaudeCodeAgentBackend.BuildArguments(request, ClaudeCodeOptions.Default());

        AssertFlagFollowedBy(args, "--resume", "sess-1");
        AssertFlagFollowedBy(args, "--append-system-prompt", "You are helpful.");
        AssertFlagFollowedBy(args, "--model", "sonnet");
    }

    [Theory]
    [InlineData(AgentPermissionMode.Full, "bypassPermissions")]
    [InlineData(AgentPermissionMode.ReadOnly, "plan")]
    [InlineData(AgentPermissionMode.Confirm, "default")]
    public void BuildArguments_Maps_The_Permission_Mode(AgentPermissionMode mode, string expected)
    {
        var args = ClaudeCodeAgentBackend.BuildArguments(Request(mode), ClaudeCodeOptions.Default());

        AssertFlagFollowedBy(args, "--permission-mode", expected);
    }

    [Fact]
    public void BuildArguments_Falls_Back_To_The_Configured_Model()
    {
        var options = new ClaudeCodeOptions { Model = "opus" };

        var args = ClaudeCodeAgentBackend.BuildArguments(Request(), options);

        AssertFlagFollowedBy(args, "--model", "opus");
    }

    // --- Availability & launch ---------------------------------------------------------------

    [Fact]
    public async Task CheckAvailability_Reports_Unavailable_When_The_Executable_Is_Missing()
    {
        var runner = new ScriptedProcessRunner();
        var backend = new ClaudeCodeAgentBackend(runner, new ClaudeCodeOptions { Executable = "aimon-nonexistent-xyz-cli" });

        var availability = await backend.CheckAvailabilityAsync(Ct);

        Assert.False(availability.IsAvailable);
        Assert.Contains("not found", availability.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(runner.LastSpec); // never tried to launch
    }

    [Fact]
    public async Task StreamAsync_Errors_Without_Launching_When_The_Executable_Is_Missing()
    {
        var runner = new ScriptedProcessRunner();
        var backend = new ClaudeCodeAgentBackend(runner, new ClaudeCodeOptions { Executable = "aimon-nonexistent-xyz-cli" });

        var items = await AsyncStream.ToListAsync(backend.StreamAsync(Request(), Ct));

        Assert.Contains(items, i => i is ProviderStreamItem.StateChange { State: AgentState.Error });
        Assert.Contains(items, i => i is ProviderStreamItem.ProviderEvent { Kind: "agent-error" });
        Assert.Null(runner.LastSpec);
    }

    [Fact]
    public async Task StreamAsync_Feeds_The_Prompt_Via_Stdin_And_Streams_Through_The_Host()
    {
        // Point the executable at a real file so resolution succeeds; the scripted runner ignores the spec.
        var options = new ClaudeCodeOptions { Executable = typeof(ClaudeCodeAgentBackendTests).Assembly.Location };
        var runner = new ScriptedProcessRunner(
            new ProcessOutput.StdOut("""{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"ok"}]},"session_id":"s9"}"""),
            new ProcessOutput.Exited(0));
        var backend = new ClaudeCodeAgentBackend(runner, options);

        var items = await AsyncStream.ToListAsync(backend.StreamAsync(Request(prompt: "fix the bug"), Ct));

        Assert.Equal(AgentState.Working, Assert.IsType<ProviderStreamItem.StateChange>(items[0]).State);
        Assert.Contains(items, i => i is ProviderStreamItem.TextDelta { Text: "ok" });
        Assert.Contains(items, i => i is ProviderStreamItem.ProviderEvent { Kind: "session", Payload: "s9" });
        Assert.Equal(AgentState.Idle, Assert.IsType<ProviderStreamItem.StateChange>(items[^1]).State);

        Assert.NotNull(runner.LastSpec);
        Assert.Equal("fix the bug", runner.LastSpec!.StandardInput);
        Assert.Equal("/repo", runner.LastSpec.WorkingDirectory);
        Assert.Contains("-p", runner.LastSpec.Arguments);
    }
}
