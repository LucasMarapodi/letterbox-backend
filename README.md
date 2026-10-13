# Backend TCC

API .NET 10 da plataforma de avaliações e reviews de jogos. Identidade, perfil, catálogo mockado, opiniões, social, notificações, dashboard, feeds e rankings da Comunidade em memória. Código exclusivamente em Back.

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

A primeira entrega da terceira fase prepara CORS para consumo direto pelo navegador: [origens, execução local e evidências](docs/CORS.md). Em Development, as origens padrão são localhost:5173 e 127.0.0.1:5173; em Production, configure `Cors:AllowedOrigins` explicitamente.

[Contratos para integração HTTP](docs/HTTP-INTEGRATION.md) e [descrição da I-PR-01](docs/I-PR-01.md).

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

`TimeProvider` é a porta de relógio nativa do .NET e pode ser substituída por host. `HostMemory` coordena o seed da identidade com exclusão mútua; contas e perfis são gravados atomicamente. Stores singleton têm estado isolado por host. Publicações e notificações compartilham escopo com lock/rollback; relações, favoritos e notas preservam unicidade sob concorrência. Reiniciar restaura fixtures e apaga gravações e tokens. IGDB é um adaptador fake, sem rede ou credenciais. Dashboard/feeds/rankings são projeções do estado existente.

Dados não persistem, tokens são simulados e não são JWT; não há SQL, integração real IGDB ou autenticação de produção. O Front continua usando mocks: esta entrega não implementa seu adaptador HTTP. Banco, autenticação de produção e integração externa exigem entregas futuras.

Confira [contratos](docs/CONTRACTS.md) e entregas [PR-1](docs/B-PR-01.md), [PR-2](docs/B-PR-02.md), [PR-3](docs/B-PR-03.md), [PR-4](docs/B-PR-04.md), [PR-5](docs/B-PR-05.md) e [PR-6](docs/B-PR-06.md).
