namespace Aimon.Domain.Conversations;

/// <summary>The author of a <see cref="Message"/> in a conversation.</summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool,
}
