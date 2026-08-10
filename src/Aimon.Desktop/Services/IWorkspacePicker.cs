namespace Aimon.Desktop.Services;

/// <summary>
/// Opens the OS folder dialog for choosing the working directory a CLI agent runs in. Behind an
/// interface so the chat view model stays free of Avalonia storage APIs (Phase 9).
/// </summary>
public interface IWorkspacePicker
{
    /// <summary>Prompts for a folder and returns its local path, or null if cancelled.</summary>
    Task<string?> PickWorkspaceAsync(CancellationToken cancellationToken = default);
}
