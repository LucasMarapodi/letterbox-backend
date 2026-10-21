using Tcc.Domain.Forum;

namespace Tcc.Domain.Tests;

public sealed class ForumTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void Empty_content_is_rejected(string? text)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainValidationException>(() => new ForumTopic(0, 1, 1, text!, "Message", now));
        Assert.Throws<DomainValidationException>(() => new ForumTopic(0, 1, 1, "Title", text!, now));
        Assert.Throws<DomainValidationException>(() => new ForumReply(0, 1, 1, text!, now));
    }

    [Fact]
    public void Limits_trim_and_UTC_are_preserved_without_nested_reply_or_duplicate_initial_message()
    {
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(-3));
        var topic = new ForumTopic(0, 1, 1, " " + new string('t', 150) + " ", " " + new string('m', 10000) + " ", now);
        var reply = new ForumReply(0, 1, 2, "  Reply  ", now);
        Assert.Equal(150, topic.Title.Length); Assert.Equal(10000, topic.Text.Length);
        Assert.Equal("Reply", reply.Text); Assert.Equal(TimeSpan.Zero, topic.CreatedAt.Offset);
        Assert.Equal(now.ToUniversalTime(), reply.CreatedAt);
        Assert.Throws<DomainValidationException>(() => new ForumTopic(0, 1, 1, new string('t', 151), "Message", now));
        Assert.Throws<DomainValidationException>(() => new ForumTopic(0, 1, 1, "Title", new string('m', 10001), now));
        Assert.Throws<DomainValidationException>(() => new ForumReply(0, 1, 1, new string('m', 10001), now));
    }
}
