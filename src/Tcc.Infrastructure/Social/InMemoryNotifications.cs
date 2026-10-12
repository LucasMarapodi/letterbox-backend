using Tcc.Application.Social;
using Tcc.Domain.Social;

namespace Tcc.Infrastructure.Social;

public sealed class InMemoryNotifications(InMemoryPublicationScope publications) : INotifications
{
    private readonly Dictionary<(NotificationType Type, string Event, string Recipient), Notification> notifications = [];
    public void AddRange(IReadOnlyList<Notification> events)
    {
        lock (publications.Gate)
        {
            foreach (var item in events)
            {
                var key = (item.Type, item.CommentId ?? item.ReviewId, item.UserId);
                if (notifications.ContainsKey(key)) continue;
                publications.OnRollback(() => notifications.Remove(key));
                notifications.Add(key, item);
            }
        }
    }
    public IReadOnlyList<Notification> ForUser(string userId)
    {
        lock (publications.Gate) return notifications.Values.Where(item => item.UserId == userId).ToArray();
    }
}
