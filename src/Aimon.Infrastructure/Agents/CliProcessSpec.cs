namespace Aimon.Infrastructure.Agents;

/// <summary>How to launch a CLI process: the executable, its arguments (already resolved for any
/// Windows shim), the working directory, extra environment variables, and optional standard input.</summary>
internal sealed record CliProcessSpec
{
    public required string FileName { get; init; }

    /// <summary>Arguments passed via <c>ArgumentList</c>, so the OS quotes each one correctly.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    /// <summary>Environment variables layered on top of the inherited environment.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Text written to the process's standard input, then closed. Null leaves stdin unopened.</summary>
    public string? StandardInput { get; init; }
}
