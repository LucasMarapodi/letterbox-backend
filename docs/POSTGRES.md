# PostgreSQL — persistência do backend

Usuários, perfis e sessões usam EF Core/Npgsql com DbContext scoped. Domain e Application não dependem do ORM. Cadastro grava conta e perfil atomicamente; índices únicos tratam a disputa por email normalizado como 409. Username é calculado no banco como `user-{id}`. PATCH bloqueia a linha da conta, aplica somente os campos enviados e confirma conta/perfil na mesma transação.

## Ambiente local

O container local `postgres-tcc` usa PostgreSQL 17, porta 5432 e banco de desenvolvimento `gamesdb`. Migrations de identidade, catálogo, opiniões, comentários/curtidas, favoritos/seguimentos e notificações foram aplicadas nesse banco, preservando os dados existentes. O arquivo ignorado `Back/.env` contém `ConnectionStrings__TccDatabase`, além das variáveis IGDB. Não há seed de usuários: execute a API em Development e registre uma conta pelo Swagger.

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

Não passar conexão por argumento de CLI. Migrations: `IdentityUsersProfiles`, `IdentitySessions`, `LocalCatalog`, `Opinions`, `CommentsAndLikes`, `FavoritesAndFollows`, `PersistentNotifications`, `ProfileCompletionLocation`, `UnifiedReviewRating`, `ReviewLikes` e `ReviewTitle`. `usuarios.id` e `sessoes_usuarios.id` são integer identity. `perfis_usuarios.usuario_id` é PK/FK integer sem identity. As FKs usam RESTRICT. Datas são timestamp with time zone; email normalizado, username e token_hash têm índices únicos.

`ReviewTitle` adiciona somente `reviews.titulo`, varchar(150) nullable, sem default/backfill ou CHECK global de título/descrição. Reviews históricas continuam com título null e suas relações/IDs preservados. A aplicação valida o título nas novas gravações; não há conversão do nome do jogo em título. Down remove somente a coluna e perde os títulos preenchidos, preservando as linhas. A S-PR-36 gerou/revisou o SQL e aplicou Up/Down apenas no banco exclusivo de testes; `gamesdb` ainda precisa receber o upgrade explicitamente antes de iniciar a nova API. [Validação e transição](S-PR-36.md).

`ProfileCompletionLocation` adiciona somente `pais` e `estado`, varchar(100) nullable em `perfis_usuarios`. Contas existentes permanecem incompletas sem perder perfil/favoritos. Não há coluna de conclusão: a regra é calculada. PUT de conclusão, PATCH de perfil e escrita comum de jogos favoritos serializam pela linha da conta. O PUT reconcilia somente diferenças na mesma transação do perfil, preservando identidade/data dos favoritos mantidos. País/estado são privados. [Contrato e validações S-PR-16](S-PR-16.md). Down remove apenas as duas colunas e perde os valores de localização; não é necessário para atualizar.

`UnifiedReviewRating` permite descrição vazia, importa uma review para cada avaliação sem review e vincula `avaliacoes(usuario_id,jogo_id)` a `reviews(usuario_id,jogo_id)` com FK RESTRICT/chave alternativa. Preserva IDs, datas e vínculos existentes; não emite eventos retroativos. Reviews sem avaliação continuam com rating null até edição pelo autor. As médias continuam exclusivamente em avaliacoes, sem duplicação. POST/PATCH e PUT legado compartilham a escrita atômica; a fonte da nota permanece única. Down preserva as reviews importadas e restaura o CHECK antigo como NOT VALID, sem apagar texto vazio. [Contrato, transição e impacto no consumidor S-PR-21](S-PR-21.md).

`ReviewLikes` amplia a modelagem para a nova relação da sexta fase: curtidas_reviews com PK composta usuario_id/review_id, FKs integer RESTRICT, criado_em UTC e índice por review_id. A modelagem atual tem 17 tabelas. INSERT com ON CONFLICT DO NOTHING mantém o vínculo/data original, DELETE remove somente o par do usuário atual e contagens usam GROUP BY em lotes, sem contador persistido. Nenhum favorito é convertido e nenhum evento é gerado. Up cria apenas a tabela/índice; Down remove somente os likes de reviews. [Contrato e validações S-PR-08](S-PR-08.md).

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

Usuários/perfis/sessões e catálogo normalizado (jogos, gêneros, plataformas e junções) estão persistidos. Reviews e avaliações persistem desde P-PR-03, com filtros e médias SQL. Comentários, respostas e curtidas persistem desde P-PR-04. Favoritos de jogos/reviews e seguimentos persistem desde P-PR-05. Notificações persistem desde P-PR-06 e são gravadas na mesma transação de review/comentário. Cache, snapshots de cobertura e token Twitch permanecem transitórios. Cache → PostgreSQL → IGDB está ativo. Detalhes em [P-PR-02.md](P-PR-02.md).

UserAccount.Id, UserSession.Id/UserId e referências de usuário nas demais entidades são int. PublicUserResponse.Id, AuthorResponse.Id, userId e actorId são JSON number; rotas de usuário aceitam inteiro positivo. Não há dados anteriores de identidade para importar, pois a fonte anterior era exclusivamente memória. Game.Id e referências gameId são int; IgdbId é long/bigint separado. Review.Id/Rating.Id e referências reviewId são int desde P-PR-03; Comment.Id/ParentCommentId e referências commentId são int/int? desde P-PR-04; Notification.Id também é integer identity desde P-PR-06. Username textual não é PK.

O Front não foi alterado. Seus tipos, validadores e parâmetros de usuário/jogo/review precisam aceitar números antes de liberar esse contrato ao consumidor atual. Login usa token opaco; somente SHA-256 é armazenado em sessoes_usuarios, nunca o token original. Senhas continuam PBKDF2 com salt.

## Catálogo P-PR-02

LocalCatalog adiciona generos, plataformas, jogos, jogos_generos e jogos_plataformas; PKs/FKs integer e referências externas bigint positivas únicas. Importações são transacionais e SQL parametrizado. Contas existentes não foram apagadas. Detalhes conhecidos persistem após reiniciar; uma busca sem cobertura de página conhecida pode exigir IGDB. O banco exclusivo de testes e seu marcador mantêm os nomes definidos na P-PR-01.

## Discussão P-PR-04

CommentsAndLikes adiciona comentarios e curtidas_comentarios. FK composta garante review do pai, trigger impede segundo nível e mudanças de vínculo após publicação. PK composta usuário/comentário protege likes concorrentes. Contagens e rankings usam SQL. Detalhes e endpoints em [P-PR-04.md](P-PR-04.md).

## Relações pessoais P-PR-05

FavoritesAndFollows adiciona jogos_favoritos, reviews_favoritas e seguimentos_usuarios com FKs específicas RESTRICT, pares únicos e CreatedAt. Feed usa EXISTS SQL, favoritos de reviews usam JOIN e os dois grupos de favoritos são lidos em RepeatableRead. Favorite.id/targetId são numéricos; targetType distingue a identidade de cada tabela. Detalhes em [P-PR-05.md](P-PR-05.md).

## Eventos P-PR-06

PersistentNotifications adiciona notificacoes, com FKs de usuário/ator/review e FK composta coerente com comentário. CHECKs por tipo/auto-notificação e índices únicos parciais impedem eventos inválidos/duplicados. Publicação e eventos usam um único DbContext e transação. Nenhum store de notificações ou lock de publicação de negócio em memória no runtime. Detalhes em [P-PR-06.md](P-PR-06.md).

## Validação final P-PR-07

FinalMigrationAuditTests reverte a cadeia completa até zero e reconstrói as 16 tabelas somente no alvo exclusivo marcado. Também aplica a fixture histórica IdentityUpgradeBaseline e verifica sua preservação no upgrade. Portanto a suíte SQL pode apagar todo conteúdo sintético de tcc_pr01_tests; não use uma base compartilhada. A guarda de nome/host/comentário é obrigatória antes da reversão. Down das migrations remove tabelas/dados da feature; não é procedimento de atualização de desenvolvimento ou produção.

FinalPersistenceFlowTests valida duas contas, conteúdo/relações e sessões após descartar o host da API mantendo o banco. 417 testes aprovados, um smoke live IGDB opt-in ignorado e build/analisadores sem avisos/erros. Nenhuma nova migration ou alteração em gamesdb foi necessária nesta PR. [Relatório](P-PR-07.md) e [matriz final](MATRIZ-PERSISTENCIA.md).
