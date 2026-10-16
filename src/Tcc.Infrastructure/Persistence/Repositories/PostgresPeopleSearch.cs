using Microsoft.EntityFrameworkCore;
using Tcc.Application.Queries;

namespace Tcc.Infrastructure.Persistence.Repositories;

public sealed class PostgresPeopleSearch(Lazy<TccDbContext> context) : IPeopleSearch
{
    public Task<PeopleSearchPage> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default) =>
        OpinionStorage.Run(async () =>
        {
            var name = Escape(request.Query);
            var username = Escape(request.Query.StartsWith('@') ? request.Query[1..] : request.Query);
            var rows = await context.Value.Users.AsNoTracking()
                .Where(user => EF.Functions.ILike(user.Profile.DisplayName, "%" + name + "%", "\\")
                    || EF.Functions.ILike(user.Username, "%" + username + "%", "\\"))
                .OrderBy(user => EF.Functions.ILike(user.Username, username, "\\") ? 0 :
                    EF.Functions.ILike(user.Username, username + "%", "\\") || EF.Functions.ILike(user.Profile.DisplayName, name + "%", "\\") ? 1 : 2)
                .ThenBy(user => user.Username).ThenBy(user => user.Id)
                .Skip(request.Offset).Take(request.PageSize + 1)
                .Select(user => new PersonSearchItem(user.Id, user.Username, user.Profile.DisplayName, user.Profile.AvatarUrl))
                .ToArrayAsync(cancellationToken);
            return new PeopleSearchPage(rows.Take(request.PageSize).ToArray(), rows.Length > request.PageSize);
        });
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
