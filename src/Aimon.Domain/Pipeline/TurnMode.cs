namespace Aimon.Domain.Pipeline;

/// <summary>
/// How a turn should be handled — the manual override the user can force, or <see cref="Auto"/> to
/// let the chat model decide (via the image-generation tool) whether to reply with text or an image.
/// </summary>
public enum TurnMode
{
    /// <summary>Let the model choose: reply with text, or call the image-generation tool.</summary>
    Auto,

    /// <summary>Force a plain text reply; the image-generation tool is not offered.</summary>
    Chat,

    /// <summary>Force image generation directly from the user's prompt, bypassing the chat model.</summary>
    Image,
}
