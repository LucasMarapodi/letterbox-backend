# Convenções HTTP

DTOs públicos são records C# nomeados, separados das entidades. JSON usa camelCase, IDs são strings opacas preservadas sem interpretação e propriedades ausentes opcionais são representadas por null explícito. DateTimeOffset é normalizado para UTC na entrada e saída, em ISO 8601. Contratos de datas sem horário serão definidos na US de catálogo.

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

Leitura de `Front/src/models/index.ts`: Id é string, DateTime é string ISO; avatarUrl, coverUrl, releaseDate, averageRating e updatedAt aceitam null. As convenções acima preservam essa representação. O frontend ainda usa mocks, e nenhuma integração HTTP foi realizada. Payloads de cadastro, perfil, catálogo e opiniões serão concretizados nas respectivas US; a fundação não cria modelos antecipados. As notificações futuras devem considerar que o Front tem campo read, mas o planejamento backend não prevê comando de leitura.

Os probes de validação e serialização existem exclusivamente no assembly de testes, adicionado pelo WebApplicationFactory; a API produtiva não carrega esse assembly.

Testes HTTP seguem a abordagem documentada pela [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).
