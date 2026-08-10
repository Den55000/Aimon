using Aimon.Domain;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.Agents;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class CliAgentProcessHostTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static CliProcessSpec Spec() => new() { FileName = "agent" };

    /// <summary>Maps each stdout line straight to a text delta, isolating host lifecycle behavior.</summary>
    private sealed class LineMapper : ICliStreamMapper
    {
        public IEnumerable<ProviderStreamItem> Map(string line) => [new ProviderStreamItem.TextDelta(line)];
    }

    [Fact]
    public async Task Wraps_Mapped_Items_With_Working_Then_Idle_On_Success()
    {
        var runner = new ScriptedProcessRunner(
            new ProcessOutput.StdOut("one"),
            new ProcessOutput.StdOut("two"),
            new ProcessOutput.Exited(0));
        var host = new CliAgentProcessHost(runner);

        var items = await AsyncStream.ToListAsync(host.StreamAsync(Spec(), new LineMapper(), Ct));

        Assert.Equal(AgentState.Working, Assert.IsType<ProviderStreamItem.StateChange>(items[0]).State);
        Assert.Equal("onetwo", string.Concat(items.OfType<ProviderStreamItem.TextDelta>().Select(d => d.Text)));
        Assert.Equal(AgentState.Idle, Assert.IsType<ProviderStreamItem.StateChange>(items[^1]).State);
    }

    [Fact]
    public async Task A_Non_Zero_Exit_Emits_Error_With_The_Stderr()
    {
        var runner = new ScriptedProcessRunner(
            new ProcessOutput.StdOut("partial"),
            new ProcessOutput.StdErr("boom happened"),
            new ProcessOutput.Exited(2));
        var host = new CliAgentProcessHost(runner);

        var items = await AsyncStream.ToListAsync(host.StreamAsync(Spec(), new LineMapper(), Ct));

        Assert.Equal(AgentState.Error, Assert.IsType<ProviderStreamItem.StateChange>(items[^2]).State);
        var error = Assert.IsType<ProviderStreamItem.ProviderEvent>(items[^1]);
        Assert.Equal("agent-error", error.Kind);
        Assert.Contains("boom happened", (string)error.Payload!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Spawn_Failure_Emits_Error()
    {
        var runner = new ScriptedProcessRunner(new ProcessOutput.Failed("claude not found"));
        var host = new CliAgentProcessHost(runner);

        var items = await AsyncStream.ToListAsync(host.StreamAsync(Spec(), new LineMapper(), Ct));

        Assert.Contains(items, i => i is ProviderStreamItem.StateChange { State: AgentState.Error });
        Assert.Contains(items, i => i is ProviderStreamItem.ProviderEvent { Kind: "agent-error", Payload: "claude not found" });
        Assert.DoesNotContain(items, i => i is ProviderStreamItem.StateChange { State: AgentState.Idle });
    }
}
