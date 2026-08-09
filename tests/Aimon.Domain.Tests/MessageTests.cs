using Aimon.Domain.Conversations;

namespace Aimon.Domain.Tests;

public class MessageTests
{
    [Fact]
    public void Factory_Helpers_Set_The_Role()
    {
        Assert.Equal(MessageRole.System, Message.System("s").Role);
        Assert.Equal(MessageRole.User, Message.User("u").Role);
        Assert.Equal(MessageRole.Assistant, Message.Assistant("a").Role);
    }

    [Fact]
    public void Rejects_Null_Content()
    {
        Assert.Throws<ArgumentNullException>(() => Message.User(null!));
    }

    [Fact]
    public void Defaults_Timestamp_To_Now_When_Not_Provided()
    {
        var before = DateTimeOffset.UtcNow;

        var message = Message.User("hi");

        Assert.True(message.Timestamp >= before);
    }

    [Fact]
    public void Preserves_A_Provided_Timestamp()
    {
        var when = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var message = new Message(MessageRole.User, "hi", timestamp: when);

        Assert.Equal(when, message.Timestamp);
    }
}
