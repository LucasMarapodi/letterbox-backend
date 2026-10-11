var builder = WebApplication.CreateBuilder(args);
builder.Services.AddExceptionHandler<Tcc.Api.IdentityExceptionHandler>();
builder.Services.AddSingleton<Tcc.Application.Identity.IPasswordHasher, Tcc.Infrastructure.Identity.Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<Tcc.Application.Identity.IUserAccounts, Tcc.Infrastructure.Identity.InMemoryUserAccounts>();
builder.Services.AddScoped<Tcc.Application.Identity.RegisterAccount>();
builder.Services.AddScoped<Tcc.Application.Identity.LoginAccount>();
builder.Services.AddScoped<Tcc.Application.Identity.SessionAccess>();
builder.Services.AddScoped<Tcc.Application.Identity.CurrentAccount>();
builder.Services.AddScoped<Tcc.Application.Identity.LogoutAccount>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Tcc.Application.Identity.ICurrentSession, Tcc.Api.Authentication.HttpCurrentSession>();
builder.Services.AddAuthentication(Tcc.Api.Authentication.MockBearerHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Tcc.Api.Authentication.MockBearerHandler>(
        Tcc.Api.Authentication.MockBearerHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Tcc.Application.Identity.IMockSessions, Tcc.Infrastructure.Identity.InMemoryMockSessions>();
builder.Services.AddOptions<Tcc.Api.Authentication.SessionOptions>()
    .BindConfiguration("Authentication")
    .Validate(options => options.SessionMinutes > 0, "SessionMinutes deve ser positivo.")
    .ValidateOnStart();
builder.Services.AddSingleton(services => new Tcc.Application.Identity.SessionSettings(TimeSpan.FromMinutes(
    services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Tcc.Api.Authentication.SessionOptions>>().Value.SessionMinutes)));
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => TypedResults.Ok(new Tcc.Api.Contracts.HealthResponse("ok")));
app.Run();

public partial class Program;
