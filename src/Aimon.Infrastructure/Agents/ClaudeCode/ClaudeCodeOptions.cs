namespace Aimon.Infrastructure.Agents.ClaudeCode;

/// <summary>
/// Configures the Claude Code backend: which executable to launch (defaults to <c>claude</c> on PATH),
/// an optional default model, and a launch timeout. Auth is the CLI's own (Phase 9 decision A2), so no
/// key lives here; a BYO key is injected via env only when present.
/// </summary>
internal sealed class ClaudeCodeOptions
{
    /// <summary>The command to launch — a name resolved on PATH, or an absolute path.</summary>
    public string Executable { get; init; } = "claude";

    /// <summary>Optional model name passed as <c>--model</c> (e.g. <c>sonnet</c>); null uses the CLI default.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Optional environment variable name whose value (an Anthropic API key) is forwarded to the CLI
    /// when the user has opted into BYO-key auth. Left to the CLI's own login by default.
    /// </summary>
    public string? ApiKeyEnvironmentVariable { get; init; }

    public static ClaudeCodeOptions Default() => new();
}
