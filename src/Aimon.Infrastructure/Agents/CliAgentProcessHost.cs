using System.Runtime.CompilerServices;
using System.Text;
using Aimon.Domain;
using Aimon.Domain.Pipeline;

namespace Aimon.Infrastructure.Agents;

/// <summary>
/// Drives a CLI agent turn generically: emits <see cref="AgentState.Working"/>, runs the process,
/// feeds each stdout line through the CLI-specific <see cref="ICliStreamMapper"/>, and closes with
/// <see cref="AgentState.Idle"/> on success or <see cref="AgentState.Error"/> + an <c>agent-error</c>
/// event on a spawn failure or non-zero exit. Reused by every CLI adapter — only the spec and mapper
/// differ. Cancellation propagates from the runner (which kills the process tree).
/// </summary>
internal sealed class CliAgentProcessHost
{
    private readonly IProcessRunner _runner;

    public CliAgentProcessHost(IProcessRunner runner) => _runner = runner;

    public async IAsyncEnumerable<ProviderStreamItem> StreamAsync(
        CliProcessSpec spec,
        ICliStreamMapper mapper,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ProviderStreamItem.StateChange(AgentState.Working);

        var stderr = new StringBuilder();
        var exitCode = 0;
        string? failure = null;

        await foreach (var output in _runner.RunAsync(spec, cancellationToken).ConfigureAwait(false))
        {
            switch (output)
            {
                case ProcessOutput.StdOut stdout:
                    foreach (var item in mapper.Map(stdout.Line))
                        yield return item;
                    break;
                case ProcessOutput.StdErr stderrLine:
                    stderr.AppendLine(stderrLine.Line);
                    break;
                case ProcessOutput.Failed failed:
                    failure = failed.Message;
                    break;
                case ProcessOutput.Exited exited:
                    exitCode = exited.ExitCode;
                    break;
            }
        }

        if (failure is not null)
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("agent-error", failure);
            yield break;
        }

        if (exitCode != 0)
        {
            var detail = stderr.Length > 0
                ? stderr.ToString().Trim()
                : $"The agent exited with code {exitCode}.";
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("agent-error", detail);
            yield break;
        }

        yield return new ProviderStreamItem.StateChange(AgentState.Idle);
    }
}
