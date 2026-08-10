using Aimon.Domain;
using Aimon.Domain.Agents;
using Aimon.Domain.Pipeline;
using Aimon.Infrastructure.Agents;
using Aimon.Infrastructure.Agents.ClaudeCode;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

/// <summary>
/// Live smoke test that launches the real Claude Code CLI. Gated on <c>AIMON_CLAUDE_LIVE=1</c> and a
/// working <c>claude</c> install (logged in), so it's a no-op by default and in CI. Exercises a
/// one-shot turn and then a session resume.
/// </summary>
[Trait("Category", "Live")]
public class ClaudeCodeLiveSmokeTests
{
    [Fact]
    public async Task Live_Runs_A_Prompt_And_Resumes_The_Session()
    {
        if (Environment.GetEnvironmentVariable("AIMON_CLAUDE_LIVE") is not "1")
            return; // Gated: don't spawn the real CLI unless explicitly opted in.

        var backend = new ClaudeCodeAgentBackend(new ProcessRunner(), ClaudeCodeOptions.Default());

        var availability = await backend.CheckAvailabilityAsync(CancellationToken.None);
        if (!availability.IsAvailable)
            return; // claude is not installed on this machine.

        var workspace = Directory.CreateTempSubdirectory("aimon-claude-live");
        try
        {
            var first = new AgentTurnRequest
            {
                Prompt = "Reply with exactly the word HELLO. Do not create or edit any files.",
                Workspace = new WorkspaceContext(workspace.FullName),
                PermissionMode = AgentPermissionMode.Full,
            };

            var firstItems = await AsyncStream.ToListAsync(backend.StreamAsync(first, CancellationToken.None));
            var firstText = string.Concat(firstItems.OfType<ProviderStreamItem.TextDelta>().Select(d => d.Text));

            Assert.False(string.IsNullOrWhiteSpace(firstText));
            Assert.Contains(firstItems, i => i is ProviderStreamItem.StateChange { State: AgentState.Idle });

            var sessionEvent = firstItems.OfType<ProviderStreamItem.ProviderEvent>().FirstOrDefault(e => e.Kind == "session");
            Assert.NotNull(sessionEvent);
            var sessionId = AgentSessionId.From((string)sessionEvent!.Payload!);

            var second = new AgentTurnRequest
            {
                Prompt = "What word did you just say? Reply with only that word.",
                Workspace = new WorkspaceContext(workspace.FullName),
                PermissionMode = AgentPermissionMode.Full,
                SessionId = sessionId,
            };

            var secondItems = await AsyncStream.ToListAsync(backend.StreamAsync(second, CancellationToken.None));
            Assert.Contains(secondItems, i => i is ProviderStreamItem.StateChange { State: AgentState.Idle });
        }
        finally
        {
            try
            {
                workspace.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temp workspace.
            }
        }
    }
}
