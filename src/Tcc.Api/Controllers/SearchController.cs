using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Application.Queries;
using Tcc.Domain.Identity;

namespace Tcc.Api.Controllers;

[ApiController, Route("api/v1/search")]
public sealed class SearchController(AggregatedSearch search) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SearchResponse>(200)]
    [ProducesResponseType<ApiError>(400)]
    [ProducesResponseType<SearchResponse>(503)]
    public async Task<ActionResult<SearchResponse>> Get([FromQuery] string? q, CancellationToken cancellationToken,
        [FromQuery] string mode = "suggestions", [FromQuery] string scope = "all", [FromQuery] int page = 1)
    {
        if (Request.Query.ContainsKey("pageSize")) throw new DomainValidationException("Tamanho de busca é fixo.");
        var result = await search.Execute(SearchRequest.Create(q, mode, scope, page), cancellationToken);
        return StatusCode(result.IsAvailable ? 200 : 503, SearchResponse.From(result));
    }
}
