# S-PR-39 — Simplificar estrutura de Application e Domain

A pasta técnica Queries reunia funcionalidades já representadas por Opinions e Social; Memory continha somente IHostSeed. Exceções usadas entre funcionalidades estavam declaradas em catálogo e identidade. A reorganização aproxima as consultas de seus contextos e torna explícitos os contratos compartilhados.

## Estrutura

Application passa de seis para quatro pastas funcionais. Domain preserva suas quatro áreas rasas:

```text
Tcc.Application/
  AggregatedSearch.cs
  PersonalDashboard.cs
  ResourceFailure.cs
  DatabaseUnavailableException.cs
  IHostSeed.cs
  Catalog/
  Identity/
  Opinions/
  Social/

Tcc.Domain/
  DomainValidationException.cs
  Catalog/
  Identity/
  Opinions/
  Social/
```

RecentReviews, CommentActivity/MostCommentedReviews/ActivityOrdering, GamesInDiscussion e MostLikedComments foram para Opinions; FollowingFeed foi para Social. AggregatedSearch, PersonalDashboard e IHostSeed foram para a raiz. Queries e Memory foram removidas de Application. ResourceFailure/ResourceFailureKind saíram de GameCatalog.cs; DatabaseUnavailableException saiu de IdentityPorts.cs; DomainValidationException saiu de Domain/Identity.

Os namespaces seguem os destinos. API, Infrastructure e testes receberam somente ajustes de referências/imports necessários, preservando DI e lifetimes, tratamento de erros, conversor SearchStatus, ICommentRanking, HostMemory e fixtures. Application continua referenciando apenas Domain; Domain continua independente. Classes, métodos, regras, assertivas comportamentais e contratos HTTP permanecem iguais.

## Verificação

Base: main `7f86c98`; branch criada antes das alterações: `refactor/sexta-39-application-domain`.

- Antes: build Release com analisadores, zero avisos/erros; 454 regressões determinísticas (399 API, 54 Domain, uma Application), mais quatro testes existentes de composição/modelo EF, todos aprovados.
- Depois: 458 testes aprovados (403 API, 54 Domain, uma Application), sem falhas ou ignorados na seleção; build Release com analisadores, zero avisos/erros. Compilação intermediária após cada grupo de movimentos também aprovada.
- Seleção: testes existentes fora de Persistence e do smoke live, mais IdentityModelTests, CatalogModelTests e BootstrapTests. Cobre identidade/perfil, catálogo/busca, opiniões/rankings, social, isolamento, memória de testes, erros, autorização, JSON/OpenAPI e fronteiras de projetos.
- Testes de modelo EF verificam HasPendingModelChanges sem conexão ao PostgreSQL. Nenhum teste SQL com reset/migration, nenhuma migration ou chamada IGDB live foi executada nesta entrega. O banco de desenvolvimento não foi acessado.
- Health, Swagger e OpenAPI: 200 antes/depois, em instância temporária separada. OpenAPI idêntico byte a byte; SHA-256 `1117285EC64395BCE395086BFE31043E20747B2A90A220CD3D5824D609BE9AB8`.
- Busca por namespaces antigos em src/tests sem ocorrências; quatro pastas funcionais em ambas as camadas. Sem diferença em entidades persistidas, DbContext, migrations/snapshot, projetos, pacotes ou OpenAPI versionado. Nenhum arquivo do Front alterado.

Comandos usados, com outputs isolados para preservar a API local em uso:

```powershell
dotnet build --no-restore -c Release -p:OutputPath=bin/SPr39/ -p:RunAnalyzers=true
dotnet test --no-restore --no-build -c Release -p:OutputPath=bin/SPr39/ --filter 'FullyQualifiedName!~Tcc.Api.Tests.Persistence&FullyQualifiedName!~LiveIgdbSmokeTests|FullyQualifiedName~IdentityModelTests|FullyQualifiedName~CatalogModelTests|FullyQualifiedName~BootstrapTests'
git diff --check
```

Logs, TRX e capturas do OpenAPI permanecem nos diretórios TestResults ignorados. Não há testes novos, RED artificial, alteração funcional, schema, pacote ou projeto novo.
