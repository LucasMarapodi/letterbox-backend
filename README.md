# Backend TCC

API .NET 10 da plataforma de avaliações e reviews de jogos. Catálogo IGDB assíncrono, cache e catálogo local em memória; usuários, perfis e sessões no PostgreSQL. Opiniões, social, notificações, dashboard, feeds e rankings seguem em transição. Código exclusivamente em Back.

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

A [G-PR-04](docs/G-PR-04.md) conecta endpoints e regras locais ao catálogo: GET /api/v1/games retorna items/page/pageSize/hasNextPage, IDs são igdb-* e metadados são expostos via headers/CORS. Configure IGDB__ClientId e IGDB__ClientSecret no ambiente Back para jogos desconhecidos; não versione credenciais. Sem configuração, consultas não atendidas localmente retornam 503 seguro. Reinicie a API após atualizar. O Front não foi alterado; adaptar seu consumidor ao envelope pertence à G-PR-05.

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
- Infrastructure: EF Core/Npgsql, hashing, integração IGDB e stores ainda transitórios.
- Api: composição, DTOs, autenticação Bearer e pipeline HTTP.
- tests: fixtures isoladas, regressões HTTP e integração com PostgreSQL real.

A [P-PR-01](docs/P-PR-01.md) persiste usuários, perfis e sessões. Cadastro conta/perfil é atômico; PATCH serializa mudanças parciais; sessões guardam somente SHA-256 do token. Reiniciar a API preserva identidade no mesmo banco. Configuração, migrations e testes em [POSTGRES.md](docs/POSTGRES.md).

`TimeProvider` pode ser substituído por host. Publicações e notificações ainda compartilham escopo em memória com lock/rollback; I/O SQL ocorre fora desses locks. Projeções carregam autores em lote. Catálogo/cache, opiniões, relações e notificações continuam transitórios até suas PRs de persistência. A ordem cache → catálogo local → IGDB foi preservada; o catálogo SQL será P-PR-02.

IDs de usuários são numéricos nos contratos. O Front precisa adaptar tipos e rotas antes da liberação; nenhum arquivo Front foi alterado. IDs das outras entidades continuam no formato atual até suas migrations. Não há importação de identidade anterior: sua fonte era apenas memória.

Confira [contratos](docs/CONTRACTS.md) e entregas [PR-1](docs/B-PR-01.md), [PR-2](docs/B-PR-02.md), [PR-3](docs/B-PR-03.md), [PR-4](docs/B-PR-04.md), [PR-5](docs/B-PR-05.md) e [PR-6](docs/B-PR-06.md).
