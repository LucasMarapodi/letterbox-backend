using Tcc.Api.Contracts;
using Tcc.Api.DependencyInjection;
using Tcc.Infrastructure.Igdb;

namespace Tcc.Api.Extensions;

public static class ApiPipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<ExternalEndpointOptions>();
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(ApiError.FromStatus(500));
        }));
        app.UseStatusCodePages(async statusContext =>
            await statusContext.HttpContext.Response.WriteAsJsonAsync(
                ApiError.FromStatus(statusContext.HttpContext.Response.StatusCode)));
        app.UseRouting();
        app.UseCors(BrowserCorsRegistration.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "TCC Games API — Swagger";
            options.SwaggerEndpoint("../openapi/v1.json", "TCC Games API v1");
            options.ConfigObject.PersistAuthorization = false;
        });
        app.MapControllers();
        app.MapOpenApi();
        app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")));
        return app;
    }
}
