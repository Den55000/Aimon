using System.Runtime.CompilerServices;
using Aimon.Application.Abstractions;
using Aimon.Application.Agents;
using Aimon.Domain;
using Aimon.Domain.Agents;
using Aimon.Domain.Pipeline;

namespace Aimon.Infrastructure.Agents.ClaudeCode;

/// <summary>
/// <see cref="IAgentBackend"/> for Anthropic's Claude Code CLI. Launches
/// <c>claude -p --output-format stream-json --verbose</c> in the workspace, resuming the session when
/// present (Phase 9 decision A4) and passing the persona as <c>--append-system-prompt</c>. Auth is the
/// CLI's own login (A2) — a BYO <c>ANTHROPIC_API_KEY</c> in the environment is inherited automatically.
/// The prompt is fed via stdin so it never hits shell argument parsing.
/// </summary>
internal sealed class ClaudeCodeAgentBackend : IAgentBackend
{
    private readonly IProcessRunner _runner;
    private readonly CliAgentProcessHost _host;
    private readonly ClaudeCodeOptions _options;

    public ClaudeCodeAgentBackend(IProcessRunner runner, ClaudeCodeOptions options)
    {
        _runner = runner;
        _host = new CliAgentProcessHost(runner);
        _options = options;
    }

    public BackendDescriptor Descriptor { get; } = new()
    {
        Id = BackendId.From("claude-code"),
        DisplayName = "Claude Code",
        Kind = BackendKind.CliAgent,
        SupportsWorkspace = true,
        SupportsSessions = true,
        SupportsImages = false,
        AuthKind = BackendAuthKind.CliNativeLogin,
    };

    public async ValueTask<BackendAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var target = CliExecutableResolver.Resolve(_options.Executable);
        if (target is null)
            return BackendAvailability.Unavailable(NotFoundMessage());

        var spec = new CliProcessSpec
        {
            FileName = target.FileName,
            Arguments = [.. target.PrefixArguments, "--version"],
        };

        string? version = null;
        var exitCode = 0;
        var failed = false;

        await foreach (var output in _runner.RunAsync(spec, cancellationToken).ConfigureAwait(false))
        {
            switch (output)
            {
                case ProcessOutput.StdOut stdout when version is null && !string.IsNullOrWhiteSpace(stdout.Line):
                    version = stdout.Line.Trim();
                    break;
                case ProcessOutput.Failed:
                    failed = true;
                    break;
                case ProcessOutput.Exited exited:
                    exitCode = exited.ExitCode;
                    break;
            }
        }

        return failed || exitCode != 0
            ? BackendAvailability.Unavailable($"'{_options.Executable}' could not be launched.")
            : BackendAvailability.Available(version);
    }

    public async IAsyncEnumerable<ProviderStreamItem> StreamAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = CliExecutableResolver.Resolve(_options.Executable);
        if (target is null)
        {
            yield return new ProviderStreamItem.StateChange(AgentState.Error);
            yield return new ProviderStreamItem.ProviderEvent("agent-error", NotFoundMessage());
            yield break;
        }

        var arguments = new List<string>(target.PrefixArguments);
        arguments.AddRange(BuildArguments(request, _options));

        var spec = new CliProcessSpec
        {
            FileName = target.FileName,
            Arguments = arguments,
            WorkingDirectory = request.Workspace.RootPath,
            StandardInput = request.Prompt,
        };

        var mapper = new ClaudeCodeStreamMapper();
        await foreach (var item in _host.StreamAsync(spec, mapper, cancellationToken).ConfigureAwait(false))
            yield return item;
    }

    /// <summary>Builds the CLI arguments for a turn (the prompt itself is passed via stdin).</summary>
    internal static IReadOnlyList<string> BuildArguments(AgentTurnRequest request, ClaudeCodeOptions options)
    {
        var arguments = new List<string>
        {
            "-p",
            "--output-format", "stream-json",
            "--verbose",
            "--permission-mode", PermissionModeValue(request.PermissionMode),
        };

        if (request.SessionId is { } session)
        {
            arguments.Add("--resume");
            arguments.Add(session.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            arguments.Add("--append-system-prompt");
            arguments.Add(request.SystemPrompt);
        }

        var model = request.Model ?? options.Model;
        if (!string.IsNullOrWhiteSpace(model))
        {
            arguments.Add("--model");
            arguments.Add(model);
        }

        return arguments;
    }

    private static string PermissionModeValue(AgentPermissionMode mode) => mode switch
    {
        AgentPermissionMode.Full => "bypassPermissions",
        AgentPermissionMode.ReadOnly => "plan",
        _ => "default",
    };

    private string NotFoundMessage()
        => $"'{_options.Executable}' was not found on PATH. Install Claude Code and run `claude login`.";
}
