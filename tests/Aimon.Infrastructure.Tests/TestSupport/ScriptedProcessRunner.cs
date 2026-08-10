using System.Runtime.CompilerServices;
using Aimon.Infrastructure.Agents;

namespace Aimon.Infrastructure.Tests.TestSupport;

/// <summary>An <see cref="IProcessRunner"/> that replays scripted output and records the last spec —
/// so the process host and Claude Code mapping are tested without spawning a real subprocess.</summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    private readonly IReadOnlyList<ProcessOutput> _outputs;

    public ScriptedProcessRunner(params ProcessOutput[] outputs) => _outputs = outputs;

    public CliProcessSpec? LastSpec { get; private set; }

    public async IAsyncEnumerable<ProcessOutput> RunAsync(
        CliProcessSpec spec,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LastSpec = spec;
        foreach (var output in _outputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return output;
        }
    }
}
