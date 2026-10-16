# PostgreSQL — P-PR-01

Usuários, perfis e sessões usam EF Core/Npgsql com DbContext scoped. Domain e Application não dependem do ORM. Cadastro grava conta e perfil atomicamente; índices únicos tratam a disputa por email normalizado como 409. Username é calculado no banco como `user-{id}`. PATCH bloqueia a linha da conta, aplica somente os campos enviados e confirma conta/perfil na mesma transação.

## Ambiente local

O container local `postgres-tcc` usa PostgreSQL 17, porta 5432 e banco de desenvolvimento `gamesdb`. As duas migrations de identidade foram aplicadas nesse banco. O arquivo ignorado `Back/.env` contém `ConnectionStrings__TccDatabase`, além das variáveis IGDB. Não há seed de usuários: execute a API em Development e registre uma conta pelo Swagger.

```powershell
dotnet run --project src/Tcc.Api --urls http://localhost:5080
```

Abra http://localhost:5080/swagger, execute POST /api/v1/auth/register e POST /api/v1/auth/login. Use o token no botão Authorize para GET /auth/me e PATCH /users/me/profile. Reinicie a API mantendo o mesmo banco: conta, perfil e sessão continuam válidos até expiração/revogação.

Development lê somente as chaves permitidas do `.env`; ambiente do processo tem prioridade. Production usa somente variáveis do processo. Não há migrations no startup. `health` verifica que a API responde, sem atestar disponibilidade SQL. Falha SQL nos fluxos de identidade retorna 503/storage_unavailable sem fallback, parâmetros ou conexão nos logs.

## Migrations explícitas

Ferramenta local em `.config/dotnet-tools.json`. A factory de design lê somente `ConnectionStrings__TccDatabase` do ambiente do processo; ela não carrega `.env` nem inicia a API. Para aplicar em um ambiente de desenvolvimento previamente identificado, disponibilize essa variável por mecanismo local seguro e execute:

```powershell
dotnet tool restore
dotnet ef database update --project src/Tcc.Infrastructure --startup-project src/Tcc.Infrastructure --configuration EfDesign
```

Não passar conexão por argumento de CLI. Migrations: `IdentityUsersProfiles` e `IdentitySessions`. `usuarios.id` e `sessoes_usuarios.id` são integer identity. `perfis_usuarios.usuario_id` é PK/FK integer sem identity. As FKs usam RESTRICT. Datas são timestamp with time zone; email normalizado, username e token_hash têm índices únicos.

EF Core omite o DeleteBehavior customizado de ownership ao gerar snapshots ([issue 23496](https://github.com/dotnet/efcore/issues/23496)). Os Designers e snapshot incluem explicitamente `WithOwner().HasForeignKey("UserId").Metadata.DeleteBehavior = DeleteBehavior.Restrict`. Revisar essa linha em novas migrations; `IdentityModelTests` exige ausência de alterações pendentes, sem suprimir o aviso.

## Testes SQL isolados

Os testes usam exclusivamente o banco local `tcc_pr01_tests`, separado de `gamesdb`. `.env.postgres-test`, ignorado pelo Git, contém somente `ConnectionStrings__TccDatabase` apontando para esse banco. Antes de migrations/TRUNCATE, o harness exige localhost, nome exato e comentário do banco `TCC exclusive automated tests P-PR-01`. Nunca aponte esse arquivo para banco compartilhado. A base e o marcador foram preparados localmente; nenhuma limpeza acontece em `gamesdb`.

```powershell
dotnet test --no-restore tests/Tcc.Api.Tests -c Release --filter FullyQualifiedName~Persistence
dotnet test --no-restore -c Release
dotnet build --no-restore -c Release -p:RunAnalyzers=true
```

Sem `.env.postgres-test`, testes marcados PostgresFact são explicitamente ignorados; isso não comprova persistência. As fixtures em memória existem apenas no assembly de testes para regressões das outras features. Os testes SQL verificam materialização, rollback, unicidade concorrente, PATCH concorrente/omissão/null, reinício, hash de token, expiração, logout isolado e FK inválida.

## Transição e compatibilidade

Somente usuários, perfis e sessões estão persistidos nesta PR. Catálogo local, reviews, notas, comentários, favoritos, seguimentos e notificações continuam em memória até suas PRs. Cache e token Twitch permanecem transitórios. A ordem cache → catálogo local → IGDB foi preservada; a etapa SQL do catálogo pertence à P-PR-02.

UserAccount.Id, UserSession.Id/UserId e referências de usuário nas demais entidades são int. PublicUserResponse.Id, AuthorResponse.Id, userId e actorId são JSON number; rotas de usuário aceitam inteiro positivo. Não há dados anteriores de identidade para importar, pois a fonte anterior era exclusivamente memória. IDs de jogos/reviews/comentários/notificações continuam strings e serão migrados com suas tabelas. Username textual não é PK.

O Front não foi alterado. Seus tipos, validadores e parâmetros de usuário precisam aceitar números antes de liberar esse contrato ao consumidor atual. Login usa token opaco; somente SHA-256 é armazenado em sessoes_usuarios, nunca o token original. Senhas continuam PBKDF2 com salt.
