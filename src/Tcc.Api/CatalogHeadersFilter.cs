using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Tcc.Application.Catalog;

namespace Tcc.Api;

public sealed class CatalogHeadersFilter(CatalogUsage usage) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (usage.Metadata is not { } metadata || context.HttpContext.Response.StatusCode >= 400) return;
        context.HttpContext.Response.Headers["X-Catalog-Stale"] = metadata.IsStale ? "true" : "false";
        context.HttpContext.Response.Headers["X-Catalog-Fetched-At"] = metadata.FetchedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
