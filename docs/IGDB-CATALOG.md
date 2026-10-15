# Catálogo assíncrono e tradução IGDB

Desde P-PR-02: cache → catálogo PostgreSQL → IGDB. IExternalGameCatalog usa IDs externos long; IAsyncGameCatalog, detalhes e cache usam IDs locais int. A identidade externa não é convertida em PK local.

## Resolução

- SearchAsync: cache da consulta → snapshot de cobertura reidratado do SQL → fornecedor quando não houver cobertura suficiente. A página externa faz upsert transacional antes do cache. Uma tabela parcial não é tratada como busca global completa.
- GetAsync/ResolveAsync: IDs locais positivos, deduplicados em lotes de até 500; cache e SQL. Ausência retorna null/404 sem adivinhar identidade externa. Dados antigos continuam disponíveis com stale, sem forçar rede.
- DiscoverGame, GET /games/igdb/{igdbId}: lookup SQL por ID externo; a ausência consulta IGDB, persiste e retorna o ID local.
- RefreshAsync: operação interna sobre um ID local conhecido; busca seu igdbId no SQL, atualiza pelo fornecedor e confirma associações antes do cache. Falha externa elegível preserva o SQL conhecido.

CancellationToken atravessa as portas. Sem rede sob lock/transação SQL, Result/Wait ou download completo do catálogo. Single-flight e limites de memória preservados. Falha SQL retorna storage_unavailable, sem fallback em memória ou sucesso falso.

## Consulta e tradução

CatalogSearch valida termo com trim (até 200, sem controles), sort permitido, page >= 1, pageSize de 1 a 100 e offset sem overflow. Sem termo: release-desc; com termo: name-asc. Também aceita name-desc. Página usa pageSize+1 para hasNextPage, sem total artificial.

Campos: id, name, summary, cover.image_id, genres.id/genres.name, platforms.id/platforms.name e first_release_date. Sem termo e em release-desc, são excluídos lançamentos ausentes/futuros. Aspas e barras do termo são escapadas; fields/endpoint/expressão não são definidos pelo usuário. Expansões seguem a [documentação oficial IGDB](https://api-docs.igdb.com/#expander).

- ID externo positivo → IgdbId long; ID local atribuído pelo PostgreSQL após commit.
- name obrigatório → title trim; summary ausente → vazio.
- cover.image_id validado → URL HTTPS fixa de imagem; ausência → null.
- gêneros/plataformas preservam ID externo e nome. Repetição consistente do ID é deduplicada; IDs ausentes/inválidos ou nomes conflitantes são rejeitados.
- lançamento Unix → DateOnly UTC; ausência → null.
- ratings externos/enums/campos extras não são importados; média usa avaliações locais.

JSON inválido, linhas/IDs repetidos ou não solicitados, identidade/nome/capa/data inválidos resultam em 502 seguro sem payload do fornecedor. Erros externos permanecem separados da sessão da plataforma.

## Fonte e cache

PostgresGameCatalog grava jogo/dimensões/vínculos na mesma transação, com unicidade por igdb_id e por par nas junções. Associações são lidas em lote sob RepeatableRead. Resposta antiga não substitui jogo mais recente; IDs locais são preservados em atualizações.

Cache/snapshots são transitórios e limitados. Snapshot contém IDs/query/metadados; os nomes e relações vêm do SQL. Após importação confirmada, cache/snapshots são invalidados; falha de gravação preserva cache anterior. O timestamp da primeira página agrega valores já persistidos, mantendo a precisão da resposta do cache.

GameResponse.id e gameId são números locais; igdbId é externo. Prefixos igdb-* não são aceitos nas rotas locais. O Front exige adaptação e não foi alterado. Testes SQL, operação e smoke real em [P-PR-02](P-PR-02.md).
