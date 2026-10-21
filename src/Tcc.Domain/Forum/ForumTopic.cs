namespace Tcc.Domain.Forum;

public sealed class ForumTopic
{
    public int Id { get; private set; }
    public int GameId { get; }
    public int UserId { get; }
    public string Title { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }

    public ForumTopic(int id, int gameId, int userId, string title, string text, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        Id = id; GameId = gameId; UserId = userId;
        Title = ForumContent.Required(title, 150);
        Text = ForumContent.Required(text, 10000);
        CreatedAt = createdAt.ToUniversalTime();
    }
}

internal static class ForumContent
{
    internal static string Required(string? value, int maximum)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum)
            throw new DomainValidationException($"Conteúdo do fórum deve ter de 1 a {maximum} caracteres.");
        return text;
    }
}
