namespace Aimon.Domain.Agents;

/// <summary>
/// The working directory a CLI agent operates in for a turn. Coding agents read and (per
/// <see cref="AgentPermissionMode"/>) modify files here, so a workspace is required whenever an
/// agent backend runs. The path is held as-is; existence and access are checked by Infrastructure
/// when the process is launched, keeping the Domain free of I/O.
/// </summary>
public sealed record WorkspaceContext
{
    public string RootPath { get; }

    public WorkspaceContext(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Workspace root path must not be blank.", nameof(rootPath));

        RootPath = rootPath.Trim();
    }

    public override string ToString() => RootPath;
}
