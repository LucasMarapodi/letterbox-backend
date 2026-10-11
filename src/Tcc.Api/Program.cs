var builder = WebApplication.CreateBuilder(args);
builder.Services.AddExceptionHandler<Tcc.Api.IdentityExceptionHandler>();
builder.Services.AddSingleton<Tcc.Application.Identity.IPasswordHasher, Tcc.Infrastructure.Identity.Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<Tcc.Application.Identity.IUserAccounts, Tcc.Infrastructure.Identity.InMemoryUserAccounts>();
builder.Services.AddScoped<Tcc.Application.Identity.RegisterAccount>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Tcc.Infrastructure.Memory.HostMemory>();
builder.Services.AddSingleton<Tcc.Application.Memory.IHostSeed>(services =>
    services.GetRequiredService<Tcc.Infrastructure.Memory.HostMemory>());
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new Tcc.Api.Contracts.UtcDateTimeOffsetConverter()));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new Tcc.Api.Contracts.UtcDateTimeOffsetConverter()))
    .ConfigureApiBehaviorOptions(options =>
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
