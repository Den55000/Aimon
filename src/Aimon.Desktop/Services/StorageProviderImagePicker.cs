using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Aimon.Desktop.Services;

/// <summary>
/// <see cref="IImagePicker"/> backed by the main window's Avalonia <see cref="IStorageProvider"/>.
/// </summary>
internal sealed class StorageProviderImagePicker : IImagePicker
{
    public async Task<IReadOnlyList<PickedImage>> PickImagesAsync(CancellationToken cancellationToken = default)
    {
        var storage = GetStorageProvider();
        if (storage is null)
            return [];

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach images",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        }).ConfigureAwait(true);

        var picked = new List<PickedImage>();
        foreach (var file in files)
        {
            await using var source = await file.OpenReadAsync().ConfigureAwait(true);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(true);
            picked.Add(new PickedImage(buffer.ToArray(), GuessMediaType(file.Name), file.Name));
        }

        return picked;
    }

    public async Task<bool> SaveImageAsync(byte[] data, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var storage = GetStorageProvider();
        if (storage is null)
            return false;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save image",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "png",
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        }).ConfigureAwait(true);

        if (file is null)
            return false;

        await using var destination = await file.OpenWriteAsync().ConfigureAwait(true);
        await destination.WriteAsync(data, cancellationToken).ConfigureAwait(true);
        return true;
    }

    private static IStorageProvider? GetStorageProvider()
        => Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window.StorageProvider
            : null;

    private static string GuessMediaType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/png",
    };
}
