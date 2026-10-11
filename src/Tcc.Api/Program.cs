var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = _ =>
        new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(Tcc.Api.Contracts.ApiError.FromStatus(400));
    options.SuppressMapClientErrors = true;
});
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(Tcc.Api.Contracts.ApiError.FromStatus(500));
}));
app.UseStatusCodePages(async statusContext =>
    await statusContext.HttpContext.Response.WriteAsJsonAsync(
        Tcc.Api.Contracts.ApiError.FromStatus(statusContext.HttpContext.Response.StatusCode)));
app.MapControllers();

app.MapGet("/health", () => TypedResults.Ok(new Tcc.Api.Contracts.HealthResponse("ok")));
app.Run();

public partial class Program;
