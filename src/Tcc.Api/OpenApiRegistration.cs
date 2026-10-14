using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tcc.Api.Contracts;

namespace Tcc.Api;

public static class OpenApiRegistration
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.ShouldInclude = description => description.RelativePath == "health" ||
                description.RelativePath?.StartsWith("api/v1/", StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new() { Title = "TCC Games API", Version = "v1",
                    Description = "Catálogo IGDB com cache e catálogo local em memória; token de sessão opaco simulado. Reiniciar descarta dados e sessões. Listagem de jogos usa envelope paginado." };
                document.Servers = [new OpenApiServer { Url = "/" }];
                document.Components ??= new();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer",
                        BearerFormat = "opaque", Description = "Token opaco do login; não é JWT. Authorization: Bearer <token>." }
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer(async (operation, context, cancellationToken) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var secured = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
                operation.Security = secured ? [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                }] : [];
                operation.Summary = $"{context.Description.HttpMethod} /{context.Description.RelativePath}";
                operation.Responses ??= new();
                if (operation.Responses.ContainsKey("201") || operation.Responses.ContainsKey("204")) operation.Responses.Remove("200");
                var errorSchema = await context.GetOrCreateSchemaAsync(typeof(ApiError), null, cancellationToken);
                context.Document?.AddComponent("ApiError", errorSchema);
                var errors = context.Description.SupportedResponseTypes.Where(response => response.StatusCode >= 400)
                    .Select(response => response.StatusCode).Concat(secured ? new[] { 401 } : []).Append(500).Distinct();
                foreach (var status in errors)
                    operation.Responses[status.ToString()] = new OpenApiResponse
                    {
                        Description = ApiError.FromStatus(status).Message,
                        Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new()
                        {
                            Schema = new OpenApiSchemaReference("ApiError", context.Document),
                            Example = JsonSerializer.SerializeToNode(ApiError.FromStatus(status), JsonSerializerOptions.Web)
                        } }
                    };
                foreach (var parameter in operation.Parameters ?? [])
                {
                    if (parameter.Schema is not OpenApiSchema schema) continue;
                    if (parameter.Name == "period") { schema.Enum = Nodes("24h", "7d", "30d"); schema.Default = JsonValue.Create("7d"); }
                    if (parameter.Name == "sort") { schema.Enum = Nodes("release-desc", "name-asc", "name-desc"); schema.Description = "Omitido: release-desc sem termo, name-asc com termo."; }
                    if (parameter.Name == "page") { schema.Minimum = "1"; schema.Default = JsonValue.Create(1); }
                    if (parameter.Name == "pageSize") { schema.Minimum = "1"; schema.Maximum = "100"; schema.Default = JsonValue.Create(20); }
                    if (parameter.Name == "kind") schema.Enum = Nodes("game", "review");
                }
                if (context.Description.RelativePath?.EndsWith("comments", StringComparison.Ordinal) == true && context.Description.HttpMethod == "POST")
                    operation.Description = "Texto obrigatório não vazio após trim; até 2.000 caracteres após trim. Autoria/data vêm da sessão/relógio. Comentário próprio é permitido.";
                else if (context.Description.HttpMethod is "POST" or "PATCH" && context.Description.RelativePath?.Contains("reviews", StringComparison.Ordinal) == true)
                    operation.Description = "Texto obrigatório não vazio após trim; até 10.000 caracteres após trim. Uma review por usuário/jogo. Apenas o autor edita; preserva identidade e publicação.";
                else if (context.Description.ActionDescriptor.EndpointMetadata.OfType<NoBodyAttribute>().Any())
                    operation.Description = "Comando idempotente, sem corpo. Identidade exclusivamente pelo Bearer; enviar corpo retorna 400.";
            });
            options.AddSchemaTransformer((schema, context, _) =>
            {
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
                if (type == typeof(DateTimeOffset))
                {
                    var nullable = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is not null || schema.Type?.HasFlag(JsonSchemaType.Null) == true;
                    schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
                    schema.Format = "date-time";
                    schema.Description = "Data UTC em ISO 8601.";
                }
                if (context.JsonPropertyInfo is not null || schema.Properties is null) return Task.CompletedTask;
                if (type == typeof(RegisterRequest) || type == typeof(LoginRequest) || type == typeof(RatingRequest) || type == typeof(TextRequest))
                {
                    schema.Required = schema.Properties.Keys.ToHashSet(StringComparer.Ordinal);
                    foreach (var property in schema.Properties.Values.OfType<OpenApiSchema>())
                        if (property.Type is not null) property.Type &= ~JsonSchemaType.Null;
                }
                if (type.Name.EndsWith("Response", StringComparison.Ordinal))
                    schema.Required = schema.Properties.Keys.Where(key => type != typeof(NotificationResponse) || key != "commentId").ToHashSet(StringComparer.Ordinal);
                if (type == typeof(RegisterRequest) || type == typeof(LoginRequest))
                {
                    if (schema.Properties["password"] is OpenApiSchema password) { password.Format = "password"; password.WriteOnly = true; }
                    schema.Examples = [type == typeof(RegisterRequest)
                        ? Example(new RegisterRequest("Exemplo", "exemplo@example.com", "senha-ficticia"))
                        : Example(new LoginRequest("exemplo@example.com", "senha-ficticia"))];
                    schema.Description = "Credenciais fictícias. Email normalizado por trim/lowercase; senha preservada, obrigatória, sem política adicional.";
                }
                if (type == typeof(RatingRequest) && schema.Properties["value"] is OpenApiSchema value)
                { value.Minimum = "0"; value.Maximum = "5"; value.Description = "Número finito entre 0 e 5, decimais permitidos."; }
                if (type == typeof(TextRequest)) schema.Examples = [Example(new TextRequest("Texto fictício."))];
                if (type == typeof(ProfilePatchRequest))
                {
                    schema.Required = new HashSet<string>();
                    schema.Description = "Campos omitidos preservam valores; displayName/bio não aceitam null, avatarUrl null limpa foto. Nome trim até 100 caracteres, bio até 2000 e avatar HTTP/HTTPS até 2048.";
                    foreach (var key in new[] { "displayName", "bio" })
                        if (schema.Properties[key] is OpenApiSchema property) property.Type = JsonSchemaType.String;
                }
                if (type == typeof(FavoriteResponse) && schema.Properties["targetType"] is OpenApiSchema kind) kind.Enum = Nodes("game", "review");
                if (type == typeof(NotificationResponse) && schema.Properties["type"] is OpenApiSchema notificationType) notificationType.Enum = Nodes("new-review", "review-comment");
                return Task.CompletedTask;
            });
        });
        return services;
    }
    private static List<JsonNode> Nodes(params string[] values) => values.Select(value => (JsonNode)JsonValue.Create(value)).ToList();
    private static JsonNode Example<T>(T input) => JsonSerializer.SerializeToNode(input, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException("Exemplo JSON ausente.");
}
