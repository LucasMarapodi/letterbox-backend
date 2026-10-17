using Tcc.Application.Catalog;
using Tcc.Domain;

namespace Tcc.Application;

public sealed record SearchRequest
{
    private SearchRequest(string query, string mode, string scope, int page)
    { Query = query; Mode = mode; Scope = scope; Page = page; }
    public string Query { get; }
    public string Mode { get; }
    public string Scope { get; }
    public int Page { get; }
    public int PageSize => Mode == "suggestions" ? 5 : 20;
    public int Offset => (Page - 1) * PageSize;
    public static SearchRequest Create(string? query, string mode = "suggestions", string scope = "all", int page = 1)
    {
        query = query?.Trim() ?? "";
        if (query.Length is < 3 or > 200 || query.Any(char.IsControl) || mode is not ("suggestions" or "results")
            || scope is not ("all" or "people" or "games") || page < 1 || (mode == "suggestions" && page != 1)
            || (long)(page - 1) * 20 > int.MaxValue)
            throw new DomainValidationException("Consulta de busca inválida; informe de 3 a 200 caracteres.");
        return new(query, mode, scope, page);
    }
}

public sealed record PersonSearchItem(int Id, string Username, string DisplayName, string? AvatarUrl);
public sealed record GameSearchItem(int Id, string Title, string? CoverUrl, string Summary, DateOnly? ReleaseDate);
public sealed record PeopleSearchPage(IReadOnlyList<PersonSearchItem> Items, bool HasNextPage);
public interface IPeopleSearch
{
    Task<PeopleSearchPage> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
public sealed record SearchError(string Code, string Message);
public enum SearchStatus { Ready, Unavailable, NotRequested }
public sealed record SearchGroup<T>(SearchStatus Status, IReadOnlyList<T> Items, int? Page = null, int? PageSize = null,
    bool? HasNextPage = null, SearchError? Error = null, CatalogMetadata? Metadata = null)
{
    public static SearchGroup<T> NotRequested() => new(SearchStatus.NotRequested, []);
    public static SearchGroup<T> Ready(IReadOnlyList<T> items, SearchRequest request, bool hasNext, CatalogMetadata? metadata = null) =>
        new(SearchStatus.Ready, items, request.Page, request.PageSize, hasNext, Metadata: metadata);
    public static SearchGroup<T> Unavailable(SearchError error) => new(SearchStatus.Unavailable, [], Error: error);
}
public sealed record SearchResult(string Query, SearchGroup<PersonSearchItem> People, SearchGroup<GameSearchItem> Games, SearchError? Error)
{
    public bool IsAvailable => People.Status == SearchStatus.Ready || Games.Status == SearchStatus.Ready;
}

public sealed class AggregatedSearch(IPeopleSearch people, IAsyncGameCatalog games)
{
    public async Task<SearchResult> Execute(SearchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var persons = SearchGroup<PersonSearchItem>.NotRequested();
        var titles = SearchGroup<GameSearchItem>.NotRequested();
        // Sources may resolve the same scoped DbContext: deliberately await each source before starting the next.
        if (request.Scope is "all" or "people")
            persons = await Run<PersonSearchItem>(async () =>
            {
                var page = await people.SearchAsync(request, cancellationToken);
                return SearchGroup<PersonSearchItem>.Ready(page.Items, request, page.HasNextPage);
            });
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Scope is "all" or "games")
            titles = await Run<GameSearchItem>(async () =>
            {
                var page = await games.SearchAsync(CatalogSearch.Create(request.Query, page: request.Page, pageSize: request.PageSize), cancellationToken);
                return SearchGroup<GameSearchItem>.Ready(page.Items.Select(item => new GameSearchItem(item.Game.Id,
                    item.Game.Title, item.Game.CoverUrl, item.Game.Summary, item.Game.ReleaseDate)).ToArray(), request, page.HasNextPage, page.Metadata);
            });
        cancellationToken.ThrowIfCancellationRequested();
        var available = persons.Status == SearchStatus.Ready || titles.Status == SearchStatus.Ready;
        return new(request.Query, persons, titles, available ? null : new("search_unavailable", "Busca temporariamente indisponível."));
    }
    private static async Task<SearchGroup<T>> Run<T>(Func<Task<SearchGroup<T>>> action)
    {
        try { return await action(); }
        catch (DatabaseUnavailableException) { return SearchGroup<T>.Unavailable(new("storage_unavailable", "Persistência temporariamente indisponível.")); }
        catch (CatalogIntegrationUnavailableException) { return SearchGroup<T>.Unavailable(new("integration_unavailable", "Integração externa indisponível.")); }
        catch (CatalogTimeoutException) { return SearchGroup<T>.Unavailable(new("integration_timeout", "Tempo limite da integração externa excedido.")); }
        catch (CatalogInvalidResponseException) { return SearchGroup<T>.Unavailable(new("integration_invalid_response", "Resposta inválida da integração externa.")); }
    }
}
