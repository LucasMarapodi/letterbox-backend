# P-PR-02 — Catálogo local e associações

O catálogo antes dependia da memória do host. Agora jogos, gêneros, plataformas e vínculos persistem em PostgreSQL. Detalhes conhecidos continuam disponíveis após reiniciar, com cache vazio e IGDB indisponível. O cache permanece transitório.

## Funcionalidade e contratos

- Busca mantém paginação/ordenação; descobertas são gravadas integralmente antes do cache.
- GameResponse.id, resumo de jogo e referências gameId são números locais int32. GameResponse.igdbId expõe separadamente o identificador externo int64.
- GET /api/v1/games/{gameId} consulta um ID local positivo; ausência retorna 404 sem inferir identidade externa.
- GET /api/v1/games/igdb/{igdbId} consulta SQL pela identidade externa; se necessário, importa da IGDB e retorna o ID local.
- Atualizações preservam o ID local e substituem associações atomicamente. Respostas mais antigas não substituem o jogo já persistido.
- Falha SQL retorna 503/storage_unavailable; não é tratada como catálogo vazio nem provoca fallback em memória/IGDB.

O Front não foi alterado. Tipos, validadores e rotas precisam aceitar IDs numéricos de usuários e jogos. IDs próprios de reviews/comentários/favoritos/notificações seguem em transição. O targetId polimórfico dos favoritos em memória permanece textual, recebendo o ID local decimal para jogos até sua normalização nas tabelas próprias.

## Código e schema

| Tabela | Modelo | Integridade |
| --- | --- | --- |
| generos | Genre | id integer identity, igdb_id bigint positivo único, nome |
| plataformas | Platform | id integer identity, igdb_id bigint positivo único, nome |
| jogos | Game | id integer identity, igdb_id bigint positivo único, dados descritivos e timestamps UTC |
| jogos_generos | GameGenre | PK jogo_id/genero_id; FKs integer RESTRICT |
| jogos_plataformas | GamePlatform | PK jogo_id/plataforma_id; FKs integer RESTRICT |

Game.Genres/Platforms são projeções de nomes ignoradas pelo EF; a fonte é composta pelas entidades normalizadas e junções. A tradução IGDB preserva IDs de gêneros/plataformas e rejeita expansões inválidas. A média continua derivada das notas locais.

PostgresGameCatalog usa EF/Npgsql e ON CONFLICT parametrizado. Jogo, dimensões e associações compartilham uma transação. Um advisory lock de transação serializa importações entre processos, limitando a concorrência de escrita nesta versão; não há rede IGDB nessa transação. Projeções usam três consultas em lote sob RepeatableRead.

ScopedSqlLocalGameCatalog cria escopos por operação para cache/single-flight compartilhados. Nenhum DbContext é singleton. O catálogo local em memória foi movido ao assembly de testes.

Cache e snapshots têm limites/TTL. Snapshots guardam somente IDs e metadados; dados são reidratados do SQL. Um conjunto parcial de jogos não representa busca global completa. Após reiniciar, detalhes usam SQL; páginas sem cobertura conhecida podem consultar IGDB. Importações confirmadas invalidam snapshots/cache, inclusive nomes compartilhados; rollback preserva cache anterior.

O primeiro retorno agrega timestamps já confirmados pelo PostgreSQL, mantendo a mesma precisão da resposta do cache. A diferença de frações de microssegundo encontrada no smoke real recebeu teste RED e correção.

## Validação e operação

RED de modelo/SQL antes do repositório. PostgreSQL 17 real isolado: materialização, IDs externos acima de int32, unicidade concorrente, renomeação preservando identidade, substituição de vínculos, resposta antiga, FKs, pares únicos, RESTRICT e rollback sem poluir cache. HTTP com composição SQL real: busca/importação, SQL hit sem fornecedor, reinício, ausência local e falha SQL segura.

391 testes determinísticos aprovados; smoke opt-in validado separadamente. Build Release com analisadores sem avisos/erros. Smoke real: 1 OAuth e 3 IGDB, cobrindo busca/cache/detalhe/SQL após eviction/próxima página/termo. [Evidência sanitizada](evidence/P-PR-02-live.json). Swagger/health/OpenAPI conferidos em instância local; documento exportado atualizado.

Migration LocalCatalog aplicada em tcc_pr01_tests e gamesdb. Upgrade adicionou cinco tabelas e preservou as cinco contas existentes. Sem importação de fixtures ou migrations no startup. Configuração no .env ignorado. [Operação PostgreSQL](POSTGRES.md).

Branch feat/postgres-pr-02-catalogo, P-US-08–17. Sem remoto configurado: entrega revisável localmente, sem publicação ou merge.
