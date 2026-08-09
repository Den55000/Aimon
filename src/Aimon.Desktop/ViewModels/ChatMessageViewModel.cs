using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// One rendered item in the chat transcript: a text bubble (streamed or static) or an image bubble
/// (a user-attached input image, or an assistant-generated result with a Save action).
/// </summary>
public partial class ChatMessageViewModel : ObservableObject
{
    /// <summary>Who authored the item — "You", "Aimon", or "System".</summary>
    public string Sender { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    public partial string Text { get; set; }

    public Bitmap? Image { get; }

    /// <summary>The raw image bytes, used as the Save command's parameter.</summary>
    public byte[]? ImageData { get; }

    /// <summary>Present only on assistant-generated images; drives the Save button.</summary>
    public IAsyncRelayCommand<byte[]?>? SaveImageCommand { get; }

    public bool IsImage => Image is not null;

    public bool HasText => !string.IsNullOrEmpty(Text);

    public bool CanSave => IsImage && SaveImageCommand is not null;

    private ChatMessageViewModel(
        string sender,
        string text,
        Bitmap? image = null,
        byte[]? imageData = null,
        IAsyncRelayCommand<byte[]?>? saveImageCommand = null)
    {
        Sender = sender;
        Text = text;
        Image = image;
        ImageData = imageData;
        SaveImageCommand = saveImageCommand;
    }

    public static ChatMessageViewModel FromUser(string text) => new("You", text);

    public static ChatMessageViewModel FromSystem(string text) => new("System", text);

    public static ChatMessageViewModel AssistantText(string text) => new("Aimon", text);

    public static ChatMessageViewModel UserImage(Bitmap image, byte[] data)
        => new("You", "", image, data);

    public static ChatMessageViewModel AssistantImage(Bitmap image, byte[] data, IAsyncRelayCommand<byte[]?> saveCommand)
        => new("Aimon", "", image, data, saveCommand);
}
