# Backend TCC

API .NET 10 da plataforma de avaliações e reviews de jogos. Catálogo IGDB assíncrono, cache e catálogo local em memória; identidade, opiniões, social, notificações, dashboard, feeds e rankings locais. Código exclusivamente em Back.

## Executar

Na raiz Back, com SDK 10.0.401:

```powershell
dotnet restore
dotnet test --no-restore
dotnet build --no-restore -c Release
dotnet run --project src/Tcc.Api --urls http://localhost:5080
```

`GET /health` retorna `200 {"status":"ok"}`. Endpoints de negócio estão em `/api/v1`; contratos e exemplos em [AUTH.md](docs/AUTH.md), [OPINIONS.md](docs/OPINIONS.md), [SOCIAL.md](docs/SOCIAL.md) e [FEEDS.md](docs/FEEDS.md).

`GET /openapi/v1.json` fornece OpenAPI 3.1 com 34 operações funcionais e health, schemas, erros e autenticação Bearer. [Documento exportado](docs/openapi.json) e [mapeamento para o frontend](docs/COMPATIBILITY.md).

Swagger UI em [http://localhost:5080/swagger](http://localhost:5080/swagger), usando o mesmo documento OpenAPI. Permite explorar contratos e executar chamadas; para operações privadas, faça login e informe o token no botão **Authorize**. [Guia do Swagger](docs/SWAGGER.md).

A primeira entrega da terceira fase prepara CORS para consumo direto pelo navegador: [origens, execução local e evidências](docs/CORS.md). Em Development, as origens padrão são localhost:5173 e 127.0.0.1:5173; em Production, configure `Cors:AllowedOrigins` explicitamente.

[Contratos para integração HTTP](docs/HTTP-INTEGRATION.md) e [descrição da I-PR-01](docs/I-PR-01.md).

A G-PR-01 implementa [autenticação Twitch/IGDB no servidor](docs/IGDB-AUTH.md), com token compartilhado em memória e configuração pelas variáveis do processo. Não houve validação live. [Descrição da entrega](docs/G-PR-01.md).

A [G-PR-02](docs/G-PR-02.md) implementa portas assíncronas, resolução cache/local/IGDB e [consultas e normalização](docs/IGDB-CATALOG.md).

A [G-PR-03](docs/G-PR-03.md) implementa cache/local em memória, limites de IGDB, consultas compartilhadas e fallback stale.

A [G-PR-04](docs/G-PR-04.md) conecta endpoints e regras locais ao catálogo: GET /api/v1/games retorna items/page/pageSize/hasNextPage, IDs são igdb-* e metadados são expostos via headers/CORS. Configure IGDB__ClientId e IGDB__ClientSecret no processo Back para jogos desconhecidos; não grave credenciais em arquivos. Sem configuração, consultas não atendidas localmente retornam 503 seguro. Reinicie a API após atualizar. O Front não foi alterado; adaptar seu consumidor ao envelope pertence à G-PR-05.

Para validar a entrega completa:

```powershell
dotnet test --no-restore -c Release
dotnet build --no-restore -c Release -p:RunAnalyzers=true
```

Configuração `Authentication:SessionMinutes` em src/Tcc.Api/appsettings.json, padrão 60 minutos e valor positivo. Fixture local: ana@example.com / teste123, exclusivamente para testes/desenvolvimento. Registre outras contas para simular seguidores e comentários de terceiros. Login retorna token opaco; operações privadas enviam `Authorization: Bearer <token>`.

## Camadas

- Domain: sem dependências externas; entidades serão introduzidas com seus comportamentos.
- Application: depende de Domain; porta `IHostSeed` para inicialização de fixtures.
- Infrastructure: implementação de inicialização única por host.
- Api: composição, DTOs e pipeline HTTP.
- tests: testes HTTP com WebApplicationFactory e verificação das dependências das camadas.

`TimeProvider` é a porta de relógio nativa do .NET e pode ser substituída por host. `HostMemory` coordena o seed da identidade com exclusão mútua; contas e perfis são gravados atomicamente. Stores singleton têm estado isolado por host. Publicações e notificações compartilham escopo com lock/rollback; relações, favoritos e notas preservam unicidade sob concorrência. Escritas preparam jogos antes do lock e revalidam sessão no commit. Reiniciar apaga gravações, catálogo/cache e tokens; somente a identidade simulada é inicializada. Fixtures de jogos/opiniões existem exclusivamente nos testes. Dashboard/feeds/rankings projetam jogos em lote pela ordem cache → local → IGDB.

Dados não persistem, tokens da plataforma são simulados e não são JWT; não há SQL ou autenticação de produção. A integração IGDB está ativa no backend, com testes de transporte simulado; smoke live e validação do novo contrato no navegador permanecem pendentes. A terceira fase integrou o Front por HTTP, mas esta entrega altera o envelope de catálogo e exige a adaptação prevista para G-PR-05.

Confira [contratos](docs/CONTRACTS.md) e entregas [PR-1](docs/B-PR-01.md), [PR-2](docs/B-PR-02.md), [PR-3](docs/B-PR-03.md), [PR-4](docs/B-PR-04.md), [PR-5](docs/B-PR-05.md) e [PR-6](docs/B-PR-06.md).
