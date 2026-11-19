# Descoberta local de jogos — V-PR-03

Depende de [temas do catálogo / V-PR-02](CATALOG-THEMES.md). Não adiciona migration: reutiliza jogos, avaliações, favoritos, gêneros e temas. Não altera Front, banners ou franquias e não consulta IGDB para classificar/recomendar.

## Endpoints

- `GET /api/v1/games/top-rated`: público, média local atual 0–5, pelo menos uma nota. Ordena média DESC, ratingCount DESC, lastRatedAt DESC e GameId ASC.
- `GET /api/v1/games/trending`: público, um item por jogo com avaliação. Ordena MAX(avaliacoes.atualizado_em) DESC e GameId ASC, sem janela temporal nem ranking por volume/comentários/lançamento.
- `GET /api/v1/games/recommendations/me`: Bearer obrigatório, usuário exclusivamente da sessão. Usa o catálogo local conhecido e os favoritos atuais.

Todos aceitam `page>=1`, `pageSize` entre 1 e 50, padrão20, com validação de overflow do offset. Envelope `{items,page,pageSize,hasNextPage}`; lookahead de um registro depois da agregação/ordenação/paginação SQL. Sem dados, items vazio real. Erros usam ApiError existente (400/401/503). Respostas no-store, sem cache servidor de resultados: alterações de favoritos/notas aparecem na próxima consulta.

TopRatedItem: `{game:GameResponse,averageRating,ratingCount,lastRatedAt}`. TrendingItem: `{game:GameResponse,lastRatedAt}`. O GameResponse inclui dados locais/capa/gêneros/temas e média local; IDs inteiros e instantes UTC preservados. As rotas literais não são tratadas como IDs de jogo.

## Recomendações

Score = gêneros distintos em comum + temas distintos em comum com o conjunto de favoritos do usuário. Compara IDs normalizados, sem substring e sem multiplicar vínculos em joins. Somente score positivo. Exclui favoritos atuais e jogos já avaliados pelo titular. Ordena score DESC, média local DESC com ausência de nota por último, GameId ASC. Catálogo sem metadados não produz gostos inventados; gêneros conhecidos bastam, mesmo quando faltam temas.

Item `{game,score,matchedGenres,matchedThemes}`. Envelope acrescenta `reason`: `no_preferences` sem favoritos com metadados úteis, `no_matches` sem resultados na página, null com resultados. Correspondências contêm somente nomes de gêneros/temas, sem lista de favoritos, e-mail ou dados de outra conta. Não recebe userId para selecionar o titular.

Agregações e exclusões são feitas no PostgreSQL antes de OFFSET/LIMIT. Metadados dos jogos da página são resolvidos em lote pelo catálogo SQL, sem uma consulta IGDB por card. Falha SQL é erro real, sem fallback para lista genérica ou store em memória.

## Data efetiva da nota

`Review.EditRated` preserva Rating.UpdatedAt quando muda somente título/texto ou a mesma nota é reenviada. Criar nota ou alterar seu valor renova a data. O upsert legado PostgresRatings aplica a mesma distinção. Review.UpdatedAt continua representando a edição da review.

Não há correção de timestamps históricos: dados anteriores podem refletir edições textuais que a versão antiga registrava como atualização da avaliação. O ranking usa o valor persistido; a regra corrigida vale para novas gravações.

## Validação essencial

Foram acrescentados somente três testes de integração com PostgreSQL isolado: rankings/atividade da nota; recomendações com dois usuários e preferências mutáveis; autenticação/paginação/vazio. Reutilizados os testes existentes de permissões, OpenAPI e opiniões. Sem teste IGDB live, dados reais ou migration nova. A V-PR-03 é empilhada sobre a branch da V-PR-02 enquanto a dependência aguarda merge.

Validação em 19/11/2026: 34 testes direcionados aprovados (26 API, 8 Domain), incluindo os três novos; build Release com analisadores sem avisos ou erros. Sem reexecutar a suíte completa nesta entrega. Os três novos testes foram registrados em RED antes do código, em edb7d71. SQL somente no banco exclusivo de testes. OpenAPI exportado de uma API HTTPS temporária; health, Swagger e documento responderam 200. A instância temporária foi encerrada; o banco de desenvolvimento e os arquivos do Front não foram alterados.
