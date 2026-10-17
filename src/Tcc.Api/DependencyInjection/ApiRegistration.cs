using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tcc.Api.Contracts;
using Tcc.Api.ErrorHandling;
using Tcc.Application;

namespace Tcc.Api.DependencyInjection;

public static class ApiRegistration
{
    public static IServiceCollection AddApiHttp(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        services.AddControllers()
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(ApiError.FromStatus(400));
                options.SuppressMapClientErrors = true;
            });
        return services;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Converters.Add(new UtcDateTimeOffsetConverter());
        options.Converters.Add(new JsonStringEnumConverter<SearchStatus>(JsonNamingPolicy.CamelCase, false));
    }
}
