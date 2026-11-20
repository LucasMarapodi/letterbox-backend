namespace Tcc.Api.Contracts;

public sealed record FeaturedGameDetails(int Id, string Title, string Summary, string? CoverUrl, string BannerUrl);
public sealed record FeaturedGameResponse(FeaturedGameDetails? Game, string SelectionReason);
