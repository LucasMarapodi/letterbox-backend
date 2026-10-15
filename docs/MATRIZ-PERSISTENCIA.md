# Matriz de modelagem e persistência — quinta fase

P-PR-01 e P-PR-02 validadas localmente: identidade e catálogo (oito tabelas). Demais tabelas permanecem planejadas. Cada tabela só fica concluída após suas duas US e testes em PostgreSQL real.

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
| Reviews | Review existente | P-US-18 | P-US-19 | PLANEJADO | PLANEJADO |
| Avaliacoes | Rating e RatingValue existentes | P-US-20 | P-US-21 | PLANEJADO | PLANEJADO |
| Comentarios | Comment adaptada com ParentCommentId | P-US-22 | P-US-23 | PLANEJADO | PLANEJADO |
| CurtidasComentarios | CommentLike nova entidade | P-US-24 | P-US-25 | PLANEJADO | PLANEJADO |
| JogosFavoritos | GameFavorite nova; adaptador do Favorite atual | P-US-26 | P-US-27 | PLANEJADO | PLANEJADO |
| ReviewsFavoritas | ReviewFavorite nova; adaptador do Favorite atual | P-US-28 | P-US-29 | PLANEJADO | PLANEJADO |
| SeguimentosUsuarios | Follow existente adaptada | P-US-30 | P-US-31 | PLANEJADO | PLANEJADO |
| Notificacoes | Notification existente | P-US-32 | P-US-33 | PLANEJADO | PLANEJADO |

## Evidências a adicionar na conclusão

Por tabela, registrar classe/configuração final, nome físico, migration, repositório/query usada, store em memória retirado, teste PostgreSQL e fluxo HTTP observado. Sem token, hash de senha, conexão ou dados pessoais reais.

Não marcar SQL validado apenas porque um DbSet existe, uma migration foi gerada ou um teste EF InMemory passou.

## Verificações globais pendentes

- Usuarios + PerfisUsuarios gravados atomicamente: VALIDADO, incluindo rollback SQL.
- SessoesUsuarios com token_hash e autenticação pós-restart: VALIDADO.
- Catálogo segue cache -> PostgreSQL -> IGDB: VALIDADO com SQL real e smoke live.
- Jogos/gêneros/plataformas e associações persistentes: VALIDADO.
- Reviews/notas e médias SQL, unicidade e autoria: PENDENTE.
- Comentários principais e respostas de uma camada na mesma review: PENDENTE.
- Curtidas únicas/idempotentes e contagem SQL: PENDENTE.
- Favoritos/seguimentos SQL e isolamento: PENDENTE.
- Notificações na mesma transação da publicação: PENDENTE.
- Migrations/upgrade em banco isolado e preservação de baseline: VALIDADO até LocalCatalog; cinco contas mantidas no upgrade local.
- Todos os stores de negócio em memória ausentes do runtime: PENDENTE.
- Falha PostgreSQL não cai em fixture/sucesso local: VALIDADO para identidade e catálogo.
- Testes/análises/build e PRs reais: PENDENTE.

## Limites

Cache de catálogo/query e token Twitch continuam transitórios. Dados de contas/sessões locais/conteúdo/relações/catálogo local são PostgreSQL. UI de respostas e curtidas é entrega separada.

Ao finalizar P-US-35, atualizar esta matriz e versionar cópia em Back/docs. Nenhuma linha PLANEJADO pode ser declarada pronta sem evidência.

## Evidências P-PR-01

Usuarios: UserAccountConfiguration, usuarios, IdentityUsersProfiles, PostgresUserAccounts. PerfisUsuarios: UserProfile owned em perfis_usuarios, mesma migration e repositório. SessoesUsuarios: UserSessionConfiguration, sessoes_usuarios, IdentitySessions, PostgresUserSessions. InMemoryUserAccounts/InMemoryMockSessions removidos; HostMemory sem registro no runtime.

Testes PostgresModelTests, PostgresIdentityHttpTests, ProfilePersistenceTests, SessionPersistenceTests e StorageFailureTests. Provider PostgreSQL 17 real isolado; desenvolvimento gamesdb com duas migrations, testes separados em tcc_pr01_tests. Configuração privada ignorada; sem segredos neste documento. Detalhes em POSTGRES.md e P-PR-01.md.

## Evidências P-PR-02

Game/Genre/Platform/GameGenre/GamePlatform, configurações CatalogConfigurations e migration LocalCatalog. Fonte: PostgresGameCatalog por ScopedSqlLocalGameCatalog; InMemoryLocalGameCatalog está somente no assembly de testes. Projeções de nomes são derivadas das junções SQL. Testes CatalogModelTests, PostgresCatalogTests, PostgresCatalogHttpTests e regressões existentes. Smoke real de seis passos aprovado, sem segredo em evidência. Oito tabelas prontas, demais grupos em transição. Detalhes em P-PR-02.md e IGDB-CATALOG.md.
