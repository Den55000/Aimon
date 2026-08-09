using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>A pending image input, shown as a removable thumbnail before the turn is sent.</summary>
public sealed class AttachmentViewModel
{
    public Bitmap Thumbnail { get; }

    public byte[] Data { get; }

    public string MediaType { get; }

    public string FileName { get; }

    /// <summary>Removes this attachment from the pending list (wired to the chat view model).</summary>
    public IRelayCommand<AttachmentViewModel>? RemoveCommand { get; }

    public AttachmentViewModel(
        Bitmap thumbnail,
        byte[] data,
        string mediaType,
        string fileName,
        IRelayCommand<AttachmentViewModel>? removeCommand = null)
    {
        Thumbnail = thumbnail;
        Data = data;
        MediaType = mediaType;
        FileName = fileName;
        RemoveCommand = removeCommand;
    }
}
