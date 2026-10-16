# Matriz de modelagem e persistência — quinta fase

P-PR-01–07 validadas localmente: as dezesseis tabelas possuem modelo e fonte SQL, com migrations completas e fluxos persistentes verificados na P-PR-07. A integração com o Front depende da adaptação aos IDs numéricos. Não há PR publicada nem merge, pois Back não tem remoto configurado.

| Tabela | Classe .NET relacionada | US A: modelo/DbContext | US B: SQL/retirada de memória | Estado modelo | Estado SQL |
| --- | --- | --- | --- | --- | --- |
| Usuarios | UserAccount e Email | P-US-02 | P-US-03 | VALIDADO | VALIDADO |
| PerfisUsuarios | UserProfile owned por UserAccount | P-US-04 | P-US-05 | VALIDADO | VALIDADO |
| SessoesUsuarios | UserSession e SessionTicket/IUserSessions | P-US-06 | P-US-07 | VALIDADO | VALIDADO |
| Generos | Genre nova entidade | P-US-08 | P-US-09 | VALIDADO | VALIDADO |
| Plataformas | Platform nova entidade | P-US-10 | P-US-11 | VALIDADO | VALIDADO |
| Jogos | Game adaptada e metadados do catálogo | P-US-12 | P-US-13 | VALIDADO | VALIDADO |
| JogosGeneros | GameGenre nova associação | P-US-14 | P-US-15 | VALIDADO | VALIDADO |
| JogosPlataformas | GamePlatform nova associação | P-US-16 | P-US-17 | VALIDADO | VALIDADO |
| Reviews | Review existente | P-US-18 | P-US-19 | VALIDADO | VALIDADO |
| Avaliacoes | Rating e RatingValue existentes | P-US-20 | P-US-21 | VALIDADO | VALIDADO |
| Comentarios | Comment adaptada com ParentCommentId | P-US-22 | P-US-23 | VALIDADO | VALIDADO |
| CurtidasComentarios | CommentLike nova entidade | P-US-24 | P-US-25 | VALIDADO | VALIDADO |
| JogosFavoritos | GameFavorite nova; adaptador do Favorite atual | P-US-26 | P-US-27 | VALIDADO | VALIDADO |
| ReviewsFavoritas | ReviewFavorite nova; adaptador do Favorite atual | P-US-28 | P-US-29 | VALIDADO | VALIDADO |
| SeguimentosUsuarios | Follow existente adaptada | P-US-30 | P-US-31 | VALIDADO | VALIDADO |
| Notificacoes | Notification existente | P-US-32 | P-US-33 | VALIDADO | VALIDADO |

## Evidências a adicionar na conclusão

Por tabela, registrar classe/configuração final, nome físico, migration, repositório/query usada, store em memória retirado, teste PostgreSQL e fluxo HTTP observado. Sem token, hash de senha, conexão ou dados pessoais reais.

Não marcar SQL validado apenas porque um DbSet existe, uma migration foi gerada ou um teste EF InMemory passou.

## Verificações globais

- Usuarios + PerfisUsuarios gravados atomicamente: VALIDADO, incluindo rollback SQL.
- SessoesUsuarios com token_hash e autenticação pós-restart: VALIDADO.
- Catálogo segue cache -> PostgreSQL -> IGDB: VALIDADO com SQL real e smoke live.
- Jogos/gêneros/plataformas e associações persistentes: VALIDADO.
- Reviews/notas e médias SQL, unicidade e autoria: VALIDADO.
- Comentários principais e respostas de uma camada na mesma review: VALIDADO.
- Curtidas únicas/idempotentes e contagem SQL: VALIDADO.
- Favoritos/seguimentos SQL e isolamento: VALIDADO.
- Notificações na mesma transação da publicação: VALIDADO.
- Migrations/upgrade em banco isolado e preservação de baseline: VALIDADO na P-PR-07, com cadeia completa desde schema de negócio vazio e upgrade da fixture histórica de identidade. Schema real coincide com modelo nas 16 tabelas.
- Todos os stores de negócio em memória ausentes do runtime: VALIDADO; cache/snapshots/token Twitch continuam transitórios.
- Falha PostgreSQL não cai em fixture/sucesso local: VALIDADO para identidade, catálogo, opiniões, discussão, relações e notificações.
- Testes/análises/build: VALIDADO, 417 testes aprovados, um smoke live IGDB opt-in ignorado, build Release/analisadores sem avisos ou erros.
- Publicação de PR real: NÃO REALIZADA, sem remoto/base. Branches, commits e descrições locais disponíveis. Sem merge.

## Limites

Cache de catálogo/query e token Twitch continuam transitórios. Dados de contas/sessões locais/conteúdo/relações/catálogo local são PostgreSQL. UI de respostas e curtidas é entrega separada.

Ao finalizar P-US-35, atualizar esta matriz e versionar cópia em Back/docs. Nenhuma linha PLANEJADO pode ser declarada pronta sem evidência.

## Evidências P-PR-01

Usuarios: UserAccountConfiguration, usuarios, IdentityUsersProfiles, PostgresUserAccounts. PerfisUsuarios: UserProfile owned em perfis_usuarios, mesma migration e repositório. SessoesUsuarios: UserSessionConfiguration, sessoes_usuarios, IdentitySessions, PostgresUserSessions. InMemoryUserAccounts/InMemoryMockSessions removidos; HostMemory sem registro no runtime.

Testes PostgresModelTests, PostgresIdentityHttpTests, ProfilePersistenceTests, SessionPersistenceTests e StorageFailureTests. Provider PostgreSQL 17 real isolado; desenvolvimento gamesdb com duas migrations, testes separados em tcc_pr01_tests. Configuração privada ignorada; sem segredos neste documento. Detalhes em POSTGRES.md e P-PR-01.md.

## Evidências P-PR-02

Game/Genre/Platform/GameGenre/GamePlatform, configurações CatalogConfigurations e migration LocalCatalog. Fonte: PostgresGameCatalog por ScopedSqlLocalGameCatalog; InMemoryLocalGameCatalog está somente no assembly de testes. Projeções de nomes são derivadas das junções SQL. Testes CatalogModelTests, PostgresCatalogTests, PostgresCatalogHttpTests e regressões existentes. Smoke real de seis passos aprovado, sem segredo em evidência. Oito tabelas prontas, demais grupos em transição. Detalhes em P-PR-02.md e IGDB-CATALOG.md.

## Evidências P-PR-03

Review/Rating, OpinionConfigurations, migration Opinions, PostgresReviews/PostgresRatings, IReviews/IRatings assíncronos. Reviews/avaliacoes com integer identity, FKs RESTRICT, constraints e unicidade. OpinionPersistenceTests verifica SQL real, autoria, concorrência, upsert, médias, timestamps, rollback e reinício. Memória somente em fixtures para essas entidades. Notificações duráveis continuam pendentes da respectiva PR. 397 testes aprovados e build/analisadores sem avisos; Swagger/OpenAPI local atualizado. Descrição em P-PR-03.md.

## Evidências P-PR-04

Comment/CommentLike, CommentConfigurations, migration CommentsAndLikes, PostgresComments/PostgresCommentLikes, portas assíncronas. InMemoryComments somente no assembly de testes. FK composta garante mesma review; trigger impede segundo nível e alteração de vínculo publicado; likes idempotentes por PK composta. CommentPersistenceTests verifica SQL real, restart, concorrência/isolamento, constraints, rollback de preparação, rankings e migration Down/Up isolada. 405 testes validados; build/analisadores sem avisos e Swagger/OpenAPI local atualizado. Upgrade preservou 5 contas e 42 jogos. Notificações duráveis permanecem pendentes. Descrição P-PR-04.md.

## Evidências P-PR-05

GameFavorite/ReviewFavorite/Follow, SocialConfigurations, migration FavoritesAndFollows, PostgresFavorites/PostgresFollows. Tabelas jogos_favoritos/reviews_favoritas com FKs específicas e seguimentos_usuarios com PK direcional e CHECK sem auto-seguimento. Stores InMemoryFavorites/InMemoryFollows somente em testes. SocialPersistenceTests cobre SQL real, restart, concorrência/isolamento, ID/CreatedAt preservados, constraints, feed/dashboard, remoção idempotente e destinatários SQL de notificação. 409 testes aprovados; build/analisadores sem avisos, upgrade preservado e Swagger/OpenAPI local atualizado. Notificações duráveis continuam pendentes. Descrição P-PR-05.md.

## Evidências P-PR-06

Notification, NotificationConfiguration, migration PersistentNotifications, PostgresNotifications e callbacks assíncronos de IReviews/IComments. Chaves numéricas, quatro FKs RESTRICT, comentário coerente com review, CHECKs por tipo/auto-notificação e índices parciais únicos. InMemoryNotifications/InMemoryPublicationScope somente em testes. NotificationPersistenceTests prova SQL real, restart/privacidade, concorrência/deduplicação, constraints, rollback e invisibilidade antes do commit. 414 testes aprovados naquela entrega; build/analisadores sem avisos. Upgrade preservado e Swagger/OpenAPI atualizados. Descrição P-PR-06.md.

## Auditoria final P-PR-07

FinalMigrationAuditTests compara colunas/tipos/nullabilidade, constraints, colunas/destinos das FKs RESTRICT, índices/colunas/unicidade/presença de filtro e identities com PostgreSQL 17. PKs/FKs locais integer, igdb_id bigint. Sete migrations Down/Up apenas no alvo exclusivo marcado; baseline IdentityUpgradeBaseline preservada após upgrade. Testes específicos abaixo comprovam também comportamento de constraints, transações, falhas e concorrência.

| Classe/configuração | Tabela física | Migration | Fonte SQL / consulta | Teste real / fluxo HTTP |
| --- | --- | --- | --- | --- |
| UserAccount / UserAccountConfiguration | usuarios | IdentityUsersProfiles | PostgresUserAccounts | PostgresIdentityHttpTests; cadastro/login |
| UserProfile owned / UserAccountConfiguration | perfis_usuarios | IdentityUsersProfiles | PostgresUserAccounts | ProfilePersistenceTests; perfil/PATCH |
| UserSession / UserSessionConfiguration | sessoes_usuarios | IdentitySessions | PostgresUserSessions | SessionPersistenceTests; login/logout/restart |
| Genre / GenreConfiguration | generos | LocalCatalog | PostgresGameCatalog | PostgresCatalogTests; catálogo/detalhe |
| Platform / PlatformConfiguration | plataformas | LocalCatalog | PostgresGameCatalog | PostgresCatalogTests; catálogo/detalhe |
| Game / GameConfiguration | jogos | LocalCatalog | PostgresGameCatalog via ScopedSqlLocalGameCatalog | PostgresCatalogHttpTests; cache/SQL/IGDB |
| GameGenre / GameGenreConfiguration | jogos_generos | LocalCatalog | PostgresGameCatalog; junção de gêneros | PostgresCatalogTests; gêneros no detalhe |
| GamePlatform / GamePlatformConfiguration | jogos_plataformas | LocalCatalog | PostgresGameCatalog; junção de plataformas | PostgresCatalogTests; plataformas no detalhe |
| Review / ReviewConfiguration | reviews | Opinions | PostgresReviews | OpinionPersistenceTests; publicar/editar/listar |
| Rating / RatingConfiguration | avaliacoes | Opinions | PostgresRatings; médias SQL | OpinionPersistenceTests; nota/média |
| Comment / CommentConfiguration | comentarios | CommentsAndLikes | PostgresComments | CommentPersistenceTests; comentários/respostas |
| CommentLike / CommentLikeConfiguration | curtidas_comentarios | CommentsAndLikes | PostgresCommentLikes | CommentPersistenceTests; like/contagem |
| GameFavorite / GameFavoriteConfiguration | jogos_favoritos | FavoritesAndFollows | PostgresFavorites | SocialPersistenceTests; favoritos de jogos |
| ReviewFavorite / ReviewFavoriteConfiguration | reviews_favoritas | FavoritesAndFollows | PostgresFavorites; JOIN reviews | SocialPersistenceTests; favoritos de reviews |
| Follow / FollowConfiguration | seguimentos_usuarios | FavoritesAndFollows | PostgresFollows; EXISTS feed | SocialPersistenceTests; seguir/feed/rede |
| Notification / NotificationConfiguration | notificacoes | PersistentNotifications | PostgresNotifications; filtro destinatário | NotificationPersistenceTests; eventos privados |

FinalPersistenceFlowTests percorre duas contas, popula as 16 tabelas, descarta a API e compara respostas com novo host/banco mantido. Sessões válidas permanecem e a sessão revogada retorna 401. Verifica conteúdo, média, hierarquia/likes, favoritos, rede, feed e notificações privadas. Nenhuma chamada IGDB no fluxo conhecido. Auditoria de DI confirma as nove portas scoped SQL, adaptador de catálogo SQL e cache compartilhado; Domain/Application sem EF/Npgsql. HostMemory legado sem registro; stores de negócio em memória somente em fixtures de testes.

417 testes aprovados (27 Domain, 1 Application, 389 API), apenas live IGDB opt-in ignorado. Build/analisadores sem avisos/erros. Health/Swagger/OpenAPI 200, sete schemas públicos com id integer, igdbId int64 e notificações sem sessão 401 em instância temporária encerrada após a conferência. Desenvolvimento consultado somente por leitura nesta PR: 16 tabelas de negócio, sete migrations, seis contas, 43 jogos, uma review. Nenhum reset ou upgrade nesse alvo.

Commit de testes ac50f56 antes da conclusão; falhas iniciais no próprio harness de auditoria foram corrigidas, sem alegar defeito RED de produção. Nenhuma nova migration foi necessária. Down remove dados das features e só foi usado no banco exclusivo marcado. Descrição e limites em P-PR-07.md. Backend validado localmente; publicação e adaptação do consumidor Front permanecem separadas.
