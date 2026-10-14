using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Tcc.Api.Contracts;

namespace Tcc.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NoBodyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var buffer = new byte[1];
        if (await context.HttpContext.Request.Body.ReadAsync(buffer, context.HttpContext.RequestAborted) > 0)
            context.Result = new BadRequestObjectResult(ApiError.FromStatus(400));
        else await next();
    }
}
