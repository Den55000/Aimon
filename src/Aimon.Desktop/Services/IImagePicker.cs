namespace Aimon.Desktop.Services;

/// <summary>An image chosen by the user for input, with its bytes and detected media type.</summary>
public sealed record PickedImage(byte[] Data, string MediaType, string FileName);

/// <summary>
/// Opens the OS file dialogs for choosing image inputs and saving a generated image. Kept behind an
/// interface so the chat view model stays free of Avalonia storage APIs.
/// </summary>
public interface IImagePicker
{
    Task<IReadOnlyList<PickedImage>> PickImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Prompts for a location and writes <paramref name="data"/> there. Returns false if cancelled.</summary>
    Task<bool> SaveImageAsync(byte[] data, string suggestedFileName, CancellationToken cancellationToken = default);
}
