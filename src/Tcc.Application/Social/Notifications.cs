using Tcc.Domain.Social;

namespace Tcc.Application.Social;

public interface INotifications
{
    void AddRange(IReadOnlyList<Notification> notifications);
    IReadOnlyList<Notification> ForUser(string userId);
}

public interface IPublicationScope
{
    T Execute<T>(Func<T> action);
}
