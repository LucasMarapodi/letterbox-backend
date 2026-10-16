# Backend TCC

API .NET 10 da plataforma de avaliações e reviews de jogos. Fundação, identidade e perfil em memória. Código exclusivamente em Back.

## Executar

Na raiz Back, com SDK 10.0.401:

```powershell
dotnet restore
dotnet test --no-restore
dotnet build --no-restore -c Release
dotnet run --project src/Tcc.Api --urls http://localhost:5080
```

`GET /health` retorna `200 {"status":"ok"}`. Cadastro, login, sessão e perfil estão em `/api/v1`; contratos e exemplos em [AUTH.md](docs/AUTH.md).

## Camadas

- Domain: sem dependências externas; entidades serão introduzidas com seus comportamentos.
- Application: depende de Domain; porta `IHostSeed` para inicialização de fixtures.
- Infrastructure: implementação de inicialização única por host.
- Api: composição, DTOs e pipeline HTTP.
- tests: testes HTTP com WebApplicationFactory e verificação das dependências das camadas.

`TimeProvider` é a porta de relógio nativa do .NET. Está registrada como singleton e pode ser substituída por host. `HostMemory` coordena o seed com exclusão mútua; a marca de inicialização só é gravada após sucesso. Contas e perfis são gravados atomicamente; sessões ficam em um store próprio. Cada host tem suas próprias instâncias, e reiniciar restaura a fixture de Ana e apaga contas criadas, edições e tokens. Ainda não há catálogo de jogos.

Confira [contratos](docs/CONTRACTS.md), [primeira PR](docs/B-PR-01.md) e [segunda PR](docs/B-PR-02.md).
