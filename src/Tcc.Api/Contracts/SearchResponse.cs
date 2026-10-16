using Tcc.Application.Queries;

namespace Tcc.Api.Contracts;

public sealed record SearchResponse(string Query, SearchGroup<PersonSearchItem> People, SearchGroup<GameSearchItem> Games, SearchError? Error)
{
    public static SearchResponse From(SearchResult result) => new(result.Query, result.People, result.Games, result.Error);
}
