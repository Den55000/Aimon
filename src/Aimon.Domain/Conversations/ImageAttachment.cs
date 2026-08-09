namespace Aimon.Domain.Conversations;

/// <summary>
/// An image attached to a <see cref="Message"/> as input (vision). Holds the raw bytes and their
/// media type so a provider adapter can encode them however its API expects (e.g. a data URL).
/// </summary>
public sealed record ImageAttachment
{
    public byte[] Data { get; }

    /// <summary>The IANA media type, e.g. <c>image/png</c> or <c>image/jpeg</c>.</summary>
    public string MediaType { get; }

    public ImageAttachment(byte[] data, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(mediaType))
            throw new ArgumentException("Media type is required.", nameof(mediaType));

        Data = data;
        MediaType = mediaType;
    }

    /// <summary>Renders the attachment as a <c>data:</c> URL (base64), the shape OpenAI's vision API accepts.</summary>
    public string ToDataUrl() => $"data:{MediaType};base64,{Convert.ToBase64String(Data)}";
}
