namespace Aimon.Infrastructure.Agents;

/// <summary>
/// One item from a running CLI process: a line of standard output, aggregated standard error, a spawn
/// failure, or the terminal exit code. A closed union so the process host can switch exhaustively.
/// </summary>
internal abstract record ProcessOutput
{
    private ProcessOutput() { }

    /// <summary>A single line written to standard output (one JSON event, for a stream-json CLI).</summary>
    public sealed record StdOut(string Line) : ProcessOutput;

    /// <summary>Text written to standard error (surfaced only if the process fails).</summary>
    public sealed record StdErr(string Line) : ProcessOutput;

    /// <summary>The process could not be started (e.g. executable not found).</summary>
    public sealed record Failed(string Message) : ProcessOutput;

    /// <summary>The process exited; carries its exit code.</summary>
    public sealed record Exited(int ExitCode) : ProcessOutput;
}
