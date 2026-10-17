# Backend TCC

API .NET 10 da plataforma de avaliações e reviews de jogos. Catálogo IGDB assíncrono, cache em memória e catálogo local PostgreSQL; usuários, perfis e sessões também persistidos. Reviews e avaliações persistem com filtros e médias SQL. Comentários, respostas e curtidas também persistem em SQL. Favoritos e seguimentos também persistem; notificações também persistem, na mesma transação de publicação. Código exclusivamente em Back.

## Executar

A [S-PR-32](docs/S-PR-32.md) unifica a edição parcial: PATCH de perfil salva nome/localização/bio/avatar e favoritos atomicamente, mesmo incompleto. Mantém campos atuais da resposta e acrescenta completion privado. Localização usa opções fechadas e preserva legado inalterado; PUT anterior permanece por compatibilidade. Modal na S-PR-33, sem migration nova.

A [S-PR-08](docs/S-PR-08.md) persiste likes de reviews: PUT/DELETE idempotentes, GET privado de likedByMe e likeCount agregado em SQL nas projeções. Curtidas são independentes de favoritos e não geram notificações. Consumo visual na S-PR-09.

A [S-PR-21](docs/S-PR-21.md) unifica publicação/edição de review e nota: POST exige rating de 0 a 5, descrição é opcional e PATCH salva ambos atomicamente. PUT legado de nota cria/atualiza a mesma review; leituras incluem rating nullable para legados. A integração do formulário pertence à S-PR-22.

A [S-PR-16](docs/S-PR-16.md) adiciona GET/PUT privados de conclusão do perfil, país/estado opcionais no banco e salvamento atômico de perfil e jogos favoritos. Conclusão exige localização, bio e três jogos distintos, com avatar opcional. O formulário permanece na S-PR-17.

A [S-PR-05](docs/S-PR-05.md) adiciona genreId/platformId locais à listagem de jogos e GET /api/v1/games/filters com opções públicas persistidas e sincronizadas pela IGDB. Filtros precedem paginação; últimos lançados exclui futuros/sem data e desempata por ID local. A interface permanece na S-PR-06.

A [S-PR-26](docs/S-PR-26.md) adiciona GET /api/v1/search público para pessoas e jogos: sugestões de cinco por grupo, resultados de 20, paginação e retry por categoria, projeções mínimas e falha parcial. Pessoas usa PostgreSQL; Jogos preserva cache → SQL → IGDB. Contrato disponível para as futuras S-PR-27/28, sem alterações no Front.

URLs externas são centralizadas por [S-PR-07](docs/S-PR-07.md): IGDB__BaseUrl, IGDB__ImagesBaseUrl e Twitch__TokenEndpoint. Configure no ambiente backend ou `.env` de Development e reinicie a API. Opções tipadas validam HTTPS na inicialização; exemplos públicos em appsettings.Development.json.

Na raiz Back, com SDK 10.0.401:

```powershell
dotnet restore
dotnet test --no-restore
dotnet build --no-restore -c Release
dotnet run --project src/Tcc.Api --urls http://localhost:5080
```

`GET /health` retorna `200 {"status":"ok"}`. Endpoints de negócio estão em `/api/v1`; contratos e exemplos em [AUTH.md](docs/AUTH.md), [OPINIONS.md](docs/OPINIONS.md), [SOCIAL.md](docs/SOCIAL.md) e [FEEDS.md](docs/FEEDS.md).

`GET /openapi/v1.json` fornece OpenAPI 3.1 com 45 operações funcionais e health, schemas, erros e autenticação Bearer. [Documento exportado](docs/openapi.json) e [mapeamento para o frontend](docs/COMPATIBILITY.md).

Swagger UI em [http://localhost:5080/swagger](http://localhost:5080/swagger), usando o mesmo documento OpenAPI. Permite explorar contratos e executar chamadas; para operações privadas, faça login e informe o token no botão **Authorize**. [Guia do Swagger](docs/SWAGGER.md).

A primeira entrega da terceira fase prepara CORS para consumo direto pelo navegador: [origens, execução local e evidências](docs/CORS.md). Em Development, as origens padrão são localhost:5173 e 127.0.0.1:5173; em Production, configure `Cors:AllowedOrigins` explicitamente.

[Contratos para integração HTTP](docs/HTTP-INTEGRATION.md) e [descrição da I-PR-01](docs/I-PR-01.md).

A G-PR-01 implementa [autenticação Twitch/IGDB no servidor](docs/IGDB-AUTH.md), com token compartilhado em memória e configuração pelas variáveis do processo. Não houve validação live. [Descrição da entrega](docs/G-PR-01.md).

A [G-PR-02](docs/G-PR-02.md) implementa portas assíncronas, resolução cache/local/IGDB e [consultas e normalização](docs/IGDB-CATALOG.md).

A [G-PR-03](docs/G-PR-03.md) implementa cache/local em memória, limites de IGDB, consultas compartilhadas e fallback stale.

A [G-PR-04](docs/G-PR-04.md) conecta endpoints e regras locais ao catálogo: GET /api/v1/games retorna items/page/pageSize/hasNextPage, naquela etapa os IDs eram igdb-* (substituídos por IDs locais numéricos na P-PR-02) e metadados são expostos via headers/CORS. Configure IGDB__ClientId e IGDB__ClientSecret no ambiente Back para jogos desconhecidos; não versione credenciais. Sem configuração, consultas não atendidas localmente retornam 503 seguro. Reinicie a API após atualizar. O Front não foi alterado; adaptar seu consumidor ao envelope pertence à G-PR-05.

Para configuração local em Development, use o arquivo `.env` ignorado pelo Git na raiz Back. Se não existir, crie `.env`, configure ConnectionStrings__TccDatabase e IGDB__ClientId/IGDB__ClientSecret e reinicie a API. Consulte [POSTGRES.md](docs/POSTGRES.md) para preparar o banco e aplicar migrations explicitamente. Valores definidos no processo têm prioridade; produção usa somente o ambiente do servidor. Não inserir credenciais em launchSettings/appsettings versionados. Consulte [IGDB-AUTH.md](docs/IGDB-AUTH.md).

Para validar a entrega completa:

```powershell
dotnet test --no-restore -c Release
dotnet build --no-restore -c Release -p:RunAnalyzers=true
```

A [G-PR-06](docs/G-PR-06.md) adiciona regressões de segurança e smoke opt-in. A suíte determinística pode excluir explicitamente o teste live com `--filter 'Category!=IgdbLive'`. Para validar Twitch/IGDB reais, execute `./scripts/igdb-smoke.ps1 -Live` após preencher o .env local; o script recusa placeholders, limita chamadas e gera evidência sanitizada. Sem -Live não acessa o fornecedor. Nenhum segredo entra nos argumentos ou relatórios.

Configuração `Authentication:SessionMinutes` em src/Tcc.Api/appsettings.json, padrão 60 minutos e valor positivo. Não há seed de conta no runtime. Registre uma conta pelo Swagger antes do login. Ana existe exclusivamente nos testes. Login retorna token opaco; operações privadas enviam `Authorization: Bearer <token>`.

## Camadas

- Domain: entidades e invariantes sem dependências externas.
- Application: casos de uso e portas assíncronas para identidade.
- Infrastructure: EF Core/Npgsql para os dados de negócio, hashing, integração IGDB e cache transitório de catálogo.
- Api: composição, DTOs, autenticação Bearer e pipeline HTTP.
- tests: fixtures isoladas, regressões HTTP e integração com PostgreSQL real.

Application e Domain mantêm quatro áreas funcionais rasas: `Catalog`, `Identity`, `Opinions` e `Social`. Consultas de reviews/comentários ficam em Opinions e o feed de seguidos em Social. Busca agregada e dashboard ficam na raiz de Application, junto de `ResourceFailure`, `DatabaseUnavailableException` e `IHostSeed`; `DomainValidationException` fica na raiz de Domain. `IHostSeed` preserva as fixtures existentes, sem stores de negócio em memória no runtime. Movimentos e validações em [S-PR-39](docs/S-PR-39.md).

A composição da API fica em `DependencyInjection`; `Configuration` mantém o leitor de ambiente e `Catalog/Adapters` reúne os adaptadores de escopo. `ApiRegistration` configura HTTP/JSON/erros globais, e `Extensions/ApiPipelineExtensions` organiza middleware e endpoints. Handlers de autenticação e erros permanecem em suas pastas. Estrutura e verificações em [S-PR-31](docs/S-PR-31.md).

A [P-PR-01](docs/P-PR-01.md) persiste usuários, perfis e sessões. Cadastro conta/perfil é atômico; PATCH serializa mudanças parciais; sessões guardam somente SHA-256 do token. Reiniciar a API preserva identidade no mesmo banco. Configuração, migrations e testes em [POSTGRES.md](docs/POSTGRES.md).

`TimeProvider` pode ser substituído por host. Reviews/comentários e notificações compartilham a mesma transação SQL. I/O SQL ocorre fora dos locks em memória. Projeções carregam autores em lote. O catálogo local agora usa PostgreSQL na [P-PR-02](docs/P-PR-02.md), mantendo cache/snapshots transitórios e a ordem cache → SQL → IGDB. Reviews/notas usam PostgreSQL desde a [P-PR-03](docs/P-PR-03.md); comentários/respostas/curtidas usam PostgreSQL desde a [P-PR-04](docs/P-PR-04.md). Favoritos e seguimentos usam PostgreSQL desde a [P-PR-05](docs/P-PR-05.md); notificações usam PostgreSQL desde a [P-PR-06](docs/P-PR-06.md), sem coordenação de negócio em memória.

IDs de usuários, jogos, reviews, avaliações, comentários, favoritos e notificações são numéricos nos contratos; jogos expõem também igdbId externo int64. GET /games/{gameId} usa ID local; GET /games/igdb/{igdbId} permite descoberta por ID externo. O Front precisa adaptar tipos/validadores/rotas; nenhum arquivo Front foi alterado. Todos os IDs locais de entidades são inteiros; tokens de autenticação permanecem textos opacos. Não há importação de identidade anterior: sua fonte era apenas memória.

Confira [contratos](docs/CONTRACTS.md) e entregas [PR-1](docs/B-PR-01.md), [PR-2](docs/B-PR-02.md), [PR-3](docs/B-PR-03.md), [PR-4](docs/B-PR-04.md), [PR-5](docs/B-PR-05.md) e [PR-6](docs/B-PR-06.md).

A [P-PR-07](docs/P-PR-07.md) conclui a validação local da persistência: 16 tabelas comparadas ao modelo EF, sete migrations reproduzidas no banco exclusivo, baseline de upgrade preservada e fluxo de duas contas após reiniciar a API. [Matriz final](docs/MATRIZ-PERSISTENCIA.md). 417 testes aprovados; build/analisadores sem avisos ou erros. A adaptação do consumidor Front aos IDs numéricos permanece em entrega própria.
