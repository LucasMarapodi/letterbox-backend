using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tcc.Api.Contracts;
using Tcc.Api.Filters;

namespace Tcc.Api.Configuration;

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
                    Description = "Usuários, perfis, sessões, catálogo, reviews, avaliações, comentários/respostas, curtidas, favoritos, seguimentos e notificações persistidos em PostgreSQL. IDs locais são inteiros; igdbId é externo de 64 bits. Publicação e eventos compartilham transação SQL. Cache → PostgreSQL → IGDB. Listagem de jogos usa envelope paginado." };
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
                var catalogOperation = context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action
                    && action.ControllerName is "Games" or "Reviews" or "Ratings" or "Favorites" or "Feed" or "Dashboard" or "Community";
                var storageOperation = secured || context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
                if (catalogOperation)
                    foreach (var response in operation.Responses.Where(item => int.TryParse(item.Key, out var code) && code is >= 200 and < 300).Select(item => item.Value).OfType<OpenApiResponse>())
                    {
                        response.Headers = new Dictionary<string, IOpenApiHeader>
                        {
                            ["X-Catalog-Stale"] = new OpenApiHeader { Description = "Presente quando o resultado utiliza catálogo; true se qualquer entrada utilizada estiver desatualizada.", Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean } },
                            ["X-Catalog-Fetched-At"] = new OpenApiHeader { Description = "Data UTC mais antiga das entradas utilizadas; reidratar cache preserva a data original.", Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" } }
                        };
                    }
                if (operation.Responses.ContainsKey("201") || operation.Responses.ContainsKey("204")) operation.Responses.Remove("200");
                var errorSchema = await context.GetOrCreateSchemaAsync(typeof(ApiError), null, cancellationToken);
                context.Document?.AddComponent("ApiError", errorSchema);
                var errors = context.Description.SupportedResponseTypes.Where(response => response.StatusCode >= 400)
                    .Select(response => response.StatusCode).Concat(secured ? new[] { 401 } : [])
                    .Concat(catalogOperation ? new[] { 400, 502, 503, 504 } : [])
                    .Concat(storageOperation ? new[] { 503 } : []).Append(500).Distinct();
                foreach (var status in errors)
                {
                    if (context.Description.RelativePath == "api/v1/search" && status == 503)
                    {
                        operation.Responses["503"].Description = "Todos os grupos solicitados indisponíveis. Mesmo envelope SearchResponse, com error.code=search_unavailable e erros públicos por grupo.";
                        continue;
                    }
                    var error = status == 503 && storageOperation && !catalogOperation
                        ? new ApiError("storage_unavailable", "Persistência temporariamente indisponível.") : ApiError.FromStatus(status);
                    operation.Responses[status.ToString()] = new OpenApiResponse
                    {
                        Description = status == 503 && storageOperation && catalogOperation
                            ? "Integração ou persistência temporariamente indisponível (integration_unavailable ou storage_unavailable)." : error.Message,
                        Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new()
                        {
                            Schema = new OpenApiSchemaReference("ApiError", context.Document),
                            Example = JsonSerializer.SerializeToNode(error, JsonSerializerOptions.Web)
                        } }
                    };
                }
                foreach (var parameter in operation.Parameters ?? [])
                {
                    if (parameter.Schema is not OpenApiSchema schema) continue;
                    if (context.Description.RelativePath == "api/v1/search")
                    {
                        if (parameter.Name == "q") { schema.MinLength = 3; schema.MaxLength = 200; if (parameter is OpenApiParameter queryParameter) queryParameter.Required = true; }
                        if (parameter.Name == "mode") { schema.Enum = Nodes("suggestions", "results"); schema.Default = JsonValue.Create("suggestions"); }
                        if (parameter.Name == "scope") { schema.Enum = Nodes("all", "people", "games"); schema.Default = JsonValue.Create("all"); }
                    }
                    if (parameter.Name == "period") { schema.Enum = Nodes("24h", "7d", "30d"); schema.Default = JsonValue.Create("7d"); }
                    if (parameter.Name == "sort") { schema.Enum = Nodes("release-desc", "name-asc", "name-desc"); schema.Description = "Omitido: release-desc sem termo, name-asc com termo."; }
                    if (parameter.Name == "page") { schema.Minimum = "1"; schema.Default = JsonValue.Create(1); }
                    if (parameter.Name == "pageSize") { schema.Minimum = "1"; schema.Maximum = "100"; schema.Default = JsonValue.Create(20); }
                    if (parameter.Name == "kind") schema.Enum = Nodes("game", "review");
                    if (parameter.Name == "userId") schema.Minimum = "1";
                    if (parameter.Name is "gameId" or "igdbId" or "reviewId" or "commentId" or "targetId") schema.Minimum = "1";
                    if (parameter.Name is "genreId" or "platformId") { schema.Minimum = "1"; schema.Description = "ID local int32 de GET /api/v1/games/filters; omissão significa Todos. Inválido/inexistente retorna 400 invalid_filter."; }
                }
                if (context.Description.RelativePath == "api/v1/games/filters")
                    operation.Description = "Opções públicas completas de gêneros/plataformas, com IDs locais int32 estáveis. Sincroniza metadados IGDB com cursor/limites e persiste no SQL; cache completo por uma hora. Não deriva opções da página exibida. Falha sem cobertura completa retorna erro, sem lista parcial como sucesso.";
                else if (context.Description.RelativePath == "api/v1/games")
                    operation.Description = "Combina termo AND gênero AND plataforma antes da paginação. genreId/platformId são IDs locais opcionais obtidos em /games/filters; inválidos/inexistentes retornam 400 invalid_filter. Sem termo: release-desc. Últimos lançados exclui futuros/sem data, ordena data decrescente e ID local crescente. Usa contagem de lançamentos mais novos para páginas avançadas e completa dias de fronteira: janela limitada a 2.000 candidatos, 20 chamadas e 30 segundos; se não comprovar o desempate, falha sem página parcial. Catálogo SQL parcial não equivale a cobertura global.";
                else if (context.Description.RelativePath == "api/v1/search")
                    operation.Description = "Busca pública por q com trim, 3–200 caracteres. suggestions: 5 por grupo e page=1; results: 20. scope=all|people|games controla fontes consultadas. Cada grupo: status ready (items e paginação válidos), unavailable (items vazio, paginação null e erro público) ou notRequested (items vazio, paginação null e error null). 200 quando ao menos um grupo solicitado está ready, inclusive vazio; 503 quando todos falham. Jogos inclui metadata fetchedAt/isStale do catálogo quando ready. Pessoas: username exato, prefixo em username/displayName, demais; desempate username/ID. Não pesquisa e-mail/bio. pageSize não é aceito.";
                else if (context.Description.RelativePath == "api/v1/games/igdb/{igdbId}")
                    operation.Description = "ID externo IGDB positivo (int64). Consulta SQL por igdb_id antes do fornecedor; importação faz upsert transacional e retorna o ID local int32 para as demais rotas.";
                else
                if (context.Description.RelativePath?.EndsWith("comments", StringComparison.Ordinal) == true && context.Description.HttpMethod == "POST")
                    operation.Description = "Texto obrigatório não vazio após trim; até 2.000 caracteres. parentCommentId omitido/null cria principal; inteiro positivo responde a principal da mesma review, sem segundo nível. Autoria vem do Bearer. Lista plana inclui parentCommentId e likeCount SQL. Vínculo de review/pai é imutável após publicação.";
                else if (context.Description.HttpMethod is "POST" or "PATCH" && context.Description.RelativePath?.Contains("reviews", StringComparison.Ordinal) == true)
                    operation.Description = "Texto obrigatório não vazio após trim; até 10.000 caracteres após trim. Uma review por usuário/jogo. Apenas o autor edita; preserva identidade e publicação.";
                else if (context.Description.ActionDescriptor.EndpointMetadata.OfType<NoBodyAttribute>().Any())
                    operation.Description = "Comando idempotente, sem corpo. Identidade exclusivamente pelo Bearer; enviar corpo retorna 400.";
            });
            options.AddSchemaTransformer((schema, context, _) =>
            {
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
                if (type == typeof(int) || type == typeof(long))
                {
                    var nullable = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is not null;
                    schema.Type = nullable ? JsonSchemaType.Integer | JsonSchemaType.Null : JsonSchemaType.Integer;
                    schema.Format = type == typeof(int) ? "int32" : "int64";
                    schema.Pattern = null;
                }
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
                if (type == typeof(CommentRequest))
                {
                    schema.Required = new HashSet<string> { "text" };
                    if (schema.Properties["text"] is OpenApiSchema text) { text.Type = JsonSchemaType.String; text.MaxLength = 2000; }
                    if (schema.Properties["parentCommentId"] is OpenApiSchema parent) parent.Minimum = "1";
                    schema.Examples = [Example(new CommentRequest("Comentário fictício.")), Example(new CommentRequest("Resposta fictícia.", 1))];
                }
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
