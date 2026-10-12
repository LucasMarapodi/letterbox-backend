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
| outros erros HTTP | http_error |

Model binding inválido em controllers usa ApiError; respostas de erro sem corpo usam o mesmo contrato. Exceções inesperadas retornam 500 seguro. Futuros casos de uso devem mapear falhas conhecidas para status e DTO nomeados. Minimal APIs futuras devem usar a mesma convenção de validação, sem depender do pipeline MVC.

## Compatibilidade com Front

Leitura de `Front/src/models/index.ts`: Id é string, DateTime é string ISO; avatarUrl, coverUrl, releaseDate, averageRating e updatedAt aceitam null. As convenções preservam essa representação. O frontend continua usando mocks. O mapeamento completo e as diferenças estão em [COMPATIBILITY.md](COMPATIBILITY.md); payloads foram concretizados nas PRs anteriores, sem integração do Front.

## OpenAPI

Documento OpenAPI 3.1 em `GET /openapi/v1.json`, público em todos os ambientes. São 34 operações funcionais mais health; o endpoint de documentação e probes de teste não entram no inventário. Schemas vêm dos DTOs, com status de sucesso/erros, exemplos fictícios, formatos de data e requisitos Bearer aplicados somente às operações privadas. As regras semânticas de trim/limites são descritas nas operações e contratos.

Cópia exportada da API executada em Release: [openapi.json](openapi.json), com servidor relativo `/` para não fixar uma porta de verificação. Não contém credenciais reais nem dados de sessão do processo. Para atualizar, execute a API e salve a resposta de `/openapi/v1.json` em Back/docs/openapi.json; o arquivo é um artefato gerado, não a fonte das rotas. Os testes OpenApiTests comparam o documento vivo ao inventário planejado e aos requisitos de schemas/acesso.

Geração usa o pacote oficial Microsoft.AspNetCore.OpenApi 10.0.12 e os [transformers documentados pela Microsoft](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0).

Os probes de validação e serialização existem exclusivamente no assembly de testes, adicionado pelo WebApplicationFactory; a API produtiva não carrega esse assembly.

Testes HTTP seguem a abordagem documentada pela [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).
