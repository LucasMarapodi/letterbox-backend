# Convenções HTTP

DTOs públicos são tipos C# nomeados, separados das entidades. JSON usa camelCase e IDs em strings opacas preservadas sem interpretação. Envelopes de ausência e propriedades nullable usam null explícito; commentId é omitido em notificações new-review. DateTimeOffset é normalizado para UTC na entrada e saída, em ISO 8601; releaseDate usa DateOnly no formato yyyy-MM-dd.

HealthResponse: `{"status":"ok"}`.

ApiError: `{"code":"validation_failed","message":"Requisição inválida."}`. Mensagens são seguras; códigos são estáveis. Não expor senhas, tokens, exceções ou stack traces nos erros.

| Status | Código |
| --- | --- |
| 400 | validation_failed |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | not_found |
| 409 | conflict |
| 500 | internal_error |
| 503 | integration_unavailable |
| 502 | integration_invalid_response |
| 504 | integration_timeout |
| outros erros HTTP | http_error |

Model binding inválido em controllers usa ApiError; respostas de erro sem corpo usam o mesmo contrato. Exceções inesperadas retornam 500 seguro. Futuros casos de uso devem mapear falhas conhecidas para status e DTO nomeados. Minimal APIs futuras devem usar a mesma convenção de validação, sem depender do pipeline MVC.

## Compatibilidade com Front

Id é string opaca, DateTime é string ISO; avatarUrl, coverUrl, releaseDate, averageRating e updatedAt aceitam null. A terceira fase integrou o Front por HTTP. A G-PR-04 mudou a listagem para envelope paginado; o adaptador dessa mudança e os avisos de stale permanecem pendentes da G-PR-05, sem arquivos Front alterados. Consulte [COMPATIBILITY.md](COMPATIBILITY.md).

GET /api/v1/games retorna GamePageResponse: items (GameResponse[]), page, pageSize e hasNextPage. Query: term, sort (release-desc/name-asc/name-desc), page (padrão 1) e pageSize (padrão 20, máximo 100). Sort omitido usa release-desc sem termo ou name-asc com termo. Não há total artificial. IDs game-* são inválidos; use igdb-{inteiro positivo}.

X-Catalog-Stale e X-Catalog-Fetched-At são expostos via CORS quando a resposta utiliza catálogo. Projeções agregam stale por qualquer entrada e fetchedAt pela mais antiga, preservando a data obtida do fornecedor. Comentários e consultas sem jogos não emitem esses headers. Erro de fornecedor não é 401 da sessão local.

## OpenAPI

Interface interativa em `/swagger`, servida pelo próprio backend em todos os ambientes, consumindo o documento existente em `/openapi/v1.json`. Operações privadas podem ser executadas após informar o token em Authorize; a autorização não é persistida após reload. Consulte [SWAGGER.md](SWAGGER.md).

Documento OpenAPI 3.1 em `GET /openapi/v1.json`, público em todos os ambientes. São 34 operações funcionais mais health; o endpoint de documentação e probes de teste não entram no inventário. Schemas vêm dos DTOs, com status de sucesso/erros, exemplos fictícios, formatos de data e requisitos Bearer aplicados somente às operações privadas. As regras semânticas de trim/limites são descritas nas operações e contratos.

Cópia exportada da API executada em Release: [openapi.json](openapi.json), com servidor relativo `/` para não fixar uma porta de verificação. Não contém credenciais reais nem dados de sessão do processo. Para atualizar, execute a API e salve a resposta de `/openapi/v1.json` em Back/docs/openapi.json; o arquivo é um artefato gerado, não a fonte das rotas. Os testes OpenApiTests comparam o documento vivo ao inventário planejado e aos requisitos de schemas/acesso.

Geração usa o pacote oficial Microsoft.AspNetCore.OpenApi 10.0.12 e os [transformers documentados pela Microsoft](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0).

Os probes de validação e serialização existem exclusivamente no assembly de testes, adicionado pelo WebApplicationFactory; a API produtiva não carrega esse assembly.

Testes HTTP seguem a abordagem documentada pela [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).
