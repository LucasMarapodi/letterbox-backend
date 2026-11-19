namespace Tcc.Api.DependencyInjection;

public static class BrowserCorsRegistration
{
    public const string PolicyName = "BrowserClient";

    public static IServiceCollection AddBrowserCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            foreach (var origin in origins)
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    || uri.Scheme != "https"
                    || origin != uri.GetLeftPart(UriPartial.Authority)
                    || !string.IsNullOrEmpty(uri.UserInfo))
                    throw new InvalidOperationException("Cors:AllowedOrigins deve conter somente origens HTTPS explícitas, sem caminhos ou wildcard.");
            }

            if (origins.Length > 0) policy.WithOrigins(origins);
            policy.WithMethods("GET", "POST", "PATCH", "PUT", "DELETE")
                .WithHeaders("Content-Type", "Authorization")
                .WithExposedHeaders("Location", "X-Catalog-Stale", "X-Catalog-Fetched-At");
        }));
        return services;
    }
}
