using Tcc.Application.Social;
using Tcc.Domain.Social;
namespace Tcc.Infrastructure.Social;

public sealed class InMemoryNotifications(InMemoryPublicationScope publications) : INotifications
{
    private readonly Dictionary<(NotificationType Type, int Event, int Recipient), Notification> notifications = [];
    private int next;
    public Task AddRangeAsync(IReadOnlyList<Notification> events, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (publications.Gate)
        {
            foreach (var item in events)
            {
                var key = (item.Type, item.CommentId ?? item.ReviewId, item.UserId);
                if (notifications.ContainsKey(key)) continue;
                publications.OnRollback(() => notifications.Remove(key));
                notifications.Add(key, item with { Id = ++next });
            }
        }
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Notification>> ForUserAsync(int userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (publications.Gate) return Task.FromResult<IReadOnlyList<Notification>>(notifications.Values.Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).ToArray());
    }
}
