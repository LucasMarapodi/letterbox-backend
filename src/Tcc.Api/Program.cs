using Tcc.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBrowserCors(builder.Configuration);
builder.Services.AddIgdbAuthentication(builder.Configuration);
builder.Services.AddCatalogMemory(builder.Configuration);
builder.Services.AddPostgresPersistence(name =>
{
    Func<string, string?> read = Environment.GetEnvironmentVariable;
    if (builder.Environment.IsDevelopment())
        read = LocalBackendEnvironment.CreateReader(LocalBackendEnvironment.PathFor(builder.Environment.ContentRootPath), read);
    return read(name);
});
builder.Services.AddIdentityFeature();
builder.Services.AddOpinionsFeature();
builder.Services.AddSocialFeature();
builder.Services.AddQueriesFeature();
builder.Services.AddApiDocumentation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new Tcc.Api.Contracts.UtcDateTimeOffsetConverter());
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<Tcc.Application.Queries.SearchStatus>(System.Text.Json.JsonNamingPolicy.CamelCase, false));
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new Tcc.Api.Contracts.UtcDateTimeOffsetConverter());
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<Tcc.Application.Queries.SearchStatus>(System.Text.Json.JsonNamingPolicy.CamelCase, false));
})
    .ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = _ =>
        new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(Tcc.Api.Contracts.ApiError.FromStatus(400));
    options.SuppressMapClientErrors = true;
});
var app = builder.Build();
_ = app.Services.GetRequiredService<Tcc.Infrastructure.Igdb.ExternalEndpointOptions>();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(Tcc.Api.Contracts.ApiError.FromStatus(500));
}));
app.UseStatusCodePages(async statusContext =>
    await statusContext.HttpContext.Response.WriteAsJsonAsync(
        Tcc.Api.Contracts.ApiError.FromStatus(statusContext.HttpContext.Response.StatusCode)));
app.UseRouting();
app.UseCors(BrowserCorsRegistration.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "TCC Games API — Swagger";
    options.SwaggerEndpoint("../openapi/v1.json", "TCC Games API v1");
    options.ConfigObject.PersistAuthorization = false;
});
app.MapControllers();
app.MapOpenApi();

app.MapGet("/health", () => TypedResults.Ok(new Tcc.Api.Contracts.HealthResponse("ok")));
app.Run();

public partial class Program;
