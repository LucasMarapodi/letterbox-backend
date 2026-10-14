namespace Tcc.Api.Contracts;

public sealed record GamePageResponse(IReadOnlyList<GameResponse> Items, int Page, int PageSize, bool HasNextPage);
