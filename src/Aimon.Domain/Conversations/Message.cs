namespace Aimon.Domain.Conversations;

/// <summary>A single message in a conversation, authored by a <see cref="MessageRole"/>.</summary>
public sealed record Message
{
    public MessageRole Role { get; }

    public string Content { get; }

    /// <summary>Optional image inputs (vision). Empty for plain text turns.</summary>
    public IReadOnlyList<ImageAttachment> Attachments { get; }

    public DateTimeOffset Timestamp { get; }

    public Message(
        MessageRole role,
        string content,
        IReadOnlyList<ImageAttachment>? attachments = null,
        DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown message role.");

        Role = role;
        Content = content;
        Attachments = attachments ?? [];
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public static Message System(string content) => new(MessageRole.System, content);

    public static Message User(string content) => new(MessageRole.User, content);

    /// <summary>A user turn carrying one or more image inputs alongside the text.</summary>
    public static Message User(string content, IReadOnlyList<ImageAttachment> attachments)
        => new(MessageRole.User, content, attachments);

    public static Message Assistant(string content) => new(MessageRole.Assistant, content);
}
