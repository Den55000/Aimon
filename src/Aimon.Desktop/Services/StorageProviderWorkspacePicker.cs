using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Aimon.Desktop.Services;

/// <summary><see cref="IWorkspacePicker"/> backed by the main window's Avalonia storage provider.</summary>
internal sealed class StorageProviderWorkspacePicker : IWorkspacePicker
{
    public async Task<string?> PickWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var storage = GetStorageProvider();
        if (storage is null)
            return null;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the agent's working folder",
            AllowMultiple = false,
        }).ConfigureAwait(true);

        var folder = folders.Count > 0 ? folders[0] : null;
        return folder?.TryGetLocalPath();
    }

    private static IStorageProvider? GetStorageProvider()
        => Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window.StorageProvider
            : null;
}
