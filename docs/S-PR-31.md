# S-PR-31 — Organização de registrations e handlers da API

Refatoração da S-US-27, branch `refactor/sexta-31-organizacao-api`, a partir de `main` (`cdbe2db`). Entrega independente: a S-PR-10, ainda em revisão na PR #28, não é dependência desta organização. Nenhum novo teste ou commit RED foi criado, conforme o escopo solicitado.

## Organização entregue

As nove registrations saíram de `Configuration` para `DependencyInjection`, com namespaces/usings atualizados. `CatalogMemoryRegistration` tornou-se `CatalogRegistration`, com entrada `AddCatalog`, pois compõe cache, SQL e acesso externo. Os demais métodos de registro existentes foram preservados.

`ScopedExternalGameCatalog`, `ScopedExternalCatalogFilters` e `ScopedSqlLocalGameCatalog` foram extraídos para arquivos próprios de `Catalog/Adapters`. Continuam internal e mantêm suas interfaces, CancellationToken e criação/descarte dos scopes. O catálogo compartilhado continua resolvendo SQL scoped por operação; nenhum DbContext passou a singleton.

`ApiRegistration.AddApiHttp` concentra o registro global de `ApiExceptionHandler`, as opções HTTP/MVC e a configuração comum de serialização. Conversores UTC e SearchStatus camelCase, validação automática 400/ApiError e SuppressMapClientErrors continuam iguais nos dois pipelines. O grupo de identidade passa a conter somente os registros ligados à identidade.

`PersistenceRegistration` oferece uma sobrecarga que recebe o ambiente do host para preservar a leitura de variáveis/arquivo local antes composta no Program. A versão que recebe o leitor de variáveis permanece disponível. `Configuration` contém somente `LocalBackendEnvironment`.

`Program.cs` passou de 63 para 22 linhas, mantendo a composição explícita de CORS, IGDB, catálogo, persistência, identidade, opiniões, social, consultas, documentação, relógio e HTTP. `ApiPipelineExtensions.UseApiPipeline` preserva a validação de startup, tratamento de exceções, status pages, routing, CORS, autenticação e autorização nessa ordem. `MapApiEndpoints` mantém Swagger, controllers, OpenAPI e health nas posições anteriores.

```text
Tcc.Api/
  Program.cs
  DependencyInjection/     registrations por responsabilidade e ApiRegistration
  Configuration/           LocalBackendEnvironment
  Catalog/Adapters/        três adaptadores de escopo em arquivos separados
  Extensions/              ApiPipelineExtensions
  Authentication/          sessão HTTP, opções e SessionBearerHandler
  ErrorHandling/           ApiExceptionHandler
  Controllers/
  Contracts/
  Filters/
```

Os handlers permanecem nas pastas de suas responsabilidades. Nenhum novo framework, scanner, pacote, projeto, regra de negócio ou migration foi introduzido. Mantidos os lifetimes, timeouts, nomes dos HttpClients, redaction/loggers, opções de configuração e a cadeia cache → PostgreSQL → IGDB. Front, Domain, Application e Infrastructure não foram alterados.

## Verificação

Antes da refatoração: 549 testes existentes aprovados (496 API, 52 Domain e um Application), somente smoke live IGDB opt-in ignorado, build Release/analisadores com zero avisos e erros.

Após a extração, o primeiro build Release/analisadores compilou todas as referências sem avisos ou erros. Os 13 arquivos de testes tocados receberam somente ajustes mecânicos de namespace/qualificação e do FullName de ScopedSqlLocalGameCatalog, mantendo todas as verificações existentes.

Verificação final após a refatoração: os mesmos 549 testes aprovados (496 API, 52 Domain e um Application), apenas smoke externo IGDB opt-in ignorado; build Release/analisadores com zero avisos e erros. `git diff --check` aprovado.

Health, Swagger e OpenAPI responderam 200 em instâncias temporárias antes e depois, encerradas após cada leitura. Os documentos OpenAPI completos foram comparados como JSON e são idênticos, sem atualizar o snapshot versionado. SHA-256 canônico: `82a4bf63bb9002cc279423ae6a0fdcb636641b5f8ccf45166cf05bc11d3a9497`.

Saída de compilação isolada com `-p:OutputPath=bin/SPr31/`, preservando a API local em execução. As verificações SQL usam somente o banco exclusivo `tcc_pr01_tests` e sua guarda existente; nenhuma operação foi executada em `gamesdb`.
