# Fundação .NET e testes

O backend não tinha solução ou infraestrutura HTTP. Esta entrega cria uma API .NET 10 com camadas Domain, Application, Infrastructure e Api, health público e base de testes isolados para as próximas funcionalidades.

## Entregas

- B-US-01: repositório exclusivo em Back, branch feat/back-pr-01-fundacao, solução src/tests e health tipado.
- B-US-02: DTOs nomeados, códigos de erro seguros, JSON inválido com 400 e serialização de IDs, null e datas UTC.
- B-US-03: porta de seed, inicialização única com lock, instâncias isoladas por host e TimeProvider substituível.

Os ciclos RED foram executados e os testes commitados antes das implementações. O health inicialmente retornou 404; erros não continham os códigos previstos; o seed não inicializava e não havia serviço de estado; datas preservavam offset local antes da normalização UTC.

## Escopo e publicação

## Validação

`dotnet test --no-restore`: 15 testes aprovados (13 HTTP/infraestrutura, 1 Domain, 1 Application). `dotnet build --no-restore -c Release`: sucesso, zero avisos e erros. `git diff --check`: sem erros. Validação executada em 11/10/2026, SDK 10.0.401.

Nenhum código do frontend alterado. Sem entidades antecipadas, banco, IGDB real, autenticação ou endpoints de negócio. A memória é suporte de inicialização; fixtures e repositórios concretos chegam nas próximas US.

O repositório local não possui remoto nem branch base com histórico aprovado. A PR remota não foi criada. É necessário definir repositório remoto e base antes de publicar. Não houve merge.
