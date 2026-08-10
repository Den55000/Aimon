namespace Aimon.Domain.Agents;

/// <summary>
/// How much autonomy a CLI agent has over the workspace for a turn. The starter release ships
/// <see cref="Full"/> (Phase 9 decision A3); the safer modes are reserved for a later refinement.
/// </summary>
public enum AgentPermissionMode
{
    /// <summary>The agent may read the workspace and propose changes, but must not write.</summary>
    ReadOnly,

    /// <summary>The agent asks for approval before each mutating action (reserved).</summary>
    Confirm,

    /// <summary>The agent may edit files and run tools freely within the workspace (default for now).</summary>
    Full,
}
