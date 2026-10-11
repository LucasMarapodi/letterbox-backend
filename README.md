# Backend TCC

Fundação .NET 10 da plataforma de avaliações e reviews de jogos. Código exclusivamente em Back.

## Executar

Na raiz Back, com SDK 10.0.401:

```powershell
dotnet restore
dotnet test --no-restore
dotnet build --no-restore -c Release
dotnet run --project src/Tcc.Api --urls http://localhost:5080
```

`GET /health` retorna `200 {"status":"ok"}`. Rotas de negócio serão adicionadas nas próximas PRs em `/api/v1`.

## Camadas

- Domain: sem dependências externas; entidades serão introduzidas com seus comportamentos.
- Application: depende de Domain; porta `IHostSeed` para inicialização de fixtures.
- Infrastructure: implementação de inicialização única por host.
- Api: composição, DTOs e pipeline HTTP.
- tests: testes HTTP com WebApplicationFactory e verificação das dependências das camadas.

`TimeProvider` é a porta de relógio nativa do .NET. Está registrada como singleton e pode ser substituída por host. `HostMemory` coordena o seed com exclusão mútua; a marca de inicialização só é gravada após sucesso. Repositórios de negócio terão seus próprios dados em memória e serão introduzidos nas próximas US. Não há fixtures de usuários ou jogos nesta fundação.

Confira [contratos](docs/CONTRACTS.md) e [descrição da primeira PR](docs/B-PR-01.md).
