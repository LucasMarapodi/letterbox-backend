namespace Tcc.Domain.Forum;

public sealed class ForumReply
{
    public int Id { get; private set; }
    public int TopicId { get; }
    public int UserId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }

    public ForumReply(int id, int topicId, int userId, string? text, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topicId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        Id = id; TopicId = topicId; UserId = userId;
        Text = ForumContent.Required(text, 10000);
        CreatedAt = createdAt.ToUniversalTime();
    }
}
