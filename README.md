# Backend TCC

API .NET 10 da plataforma de avaliações e reviews de jogos. Identidade, perfil, catálogo mockado, notas, reviews e comentários em memória. Código exclusivamente em Back.

## Executar

Na raiz Back, com SDK 10.0.401:

```powershell
dotnet restore
dotnet test --no-restore
dotnet build --no-restore -c Release
dotnet run --project src/Tcc.Api --urls http://localhost:5080
```

`GET /health` retorna `200 {"status":"ok"}`. Endpoints de negócio estão em `/api/v1`; contratos e exemplos em [AUTH.md](docs/AUTH.md) e [OPINIONS.md](docs/OPINIONS.md).

## Camadas

- Domain: sem dependências externas; entidades serão introduzidas com seus comportamentos.
- Application: depende de Domain; porta `IHostSeed` para inicialização de fixtures.
- Infrastructure: implementação de inicialização única por host.
- Api: composição, DTOs e pipeline HTTP.
- tests: testes HTTP com WebApplicationFactory e verificação das dependências das camadas.

`TimeProvider` é a porta de relógio nativa do .NET e pode ser substituída por host. `HostMemory` coordena o seed da identidade com exclusão mútua; contas e perfis são gravados atomicamente. Sessões, notas, reviews e comentários têm stores singleton próprios com estado isolado por host. Unicidade de review e substituição de nota são protegidas sob lock. Reiniciar restaura fixtures e apaga gravações e tokens. IGDB é um adaptador fake, sem rede ou credenciais; notificações chegam na próxima PR.

Confira [contratos](docs/CONTRACTS.md), [primeira PR](docs/B-PR-01.md), [segunda PR](docs/B-PR-02.md) e [terceira PR](docs/B-PR-03.md).
