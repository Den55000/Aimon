namespace Aimon.Infrastructure.Agents;

/// <summary>
/// Spawns a CLI process and streams its output. The seam that lets the process host — and the Claude
/// Code adapter's mapping — be unit-tested against scripted output with no real subprocess.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Launches the process and yields its output as it arrives, then a terminal
    /// <see cref="ProcessOutput.Exited"/> (or <see cref="ProcessOutput.Failed"/> if it can't start).
    /// Cancellation kills the process tree and surfaces as <see cref="OperationCanceledException"/>.
    /// </summary>
    IAsyncEnumerable<ProcessOutput> RunAsync(CliProcessSpec spec, CancellationToken cancellationToken);
}
