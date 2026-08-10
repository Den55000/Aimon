namespace Aimon.Application.Agents;

/// <summary>The family a chat backend belongs to.</summary>
public enum BackendKind
{
    /// <summary>A commercial model provider called over HTTP (e.g. OpenAI).</summary>
    ModelProvider,

    /// <summary>An external agentic coding CLI driven as a subprocess (e.g. Claude Code).</summary>
    CliAgent,
}
