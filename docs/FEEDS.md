# Dashboard, feeds e Comunidade — contratos atuais

Base `/api/v1`. Sem paginação ou limites silenciosos de quantidade. IDs locais numéricos e datas UTC; erros ApiError.

- GET /feed/following: privado, ReviewResponse[] dos autores atualmente seguidos pelo token, createdAt decrescente e ID numérico crescente. Deixar de seguir remove resultados na próxima consulta; autoria própria não entra automaticamente.
- GET /dashboard: privado, {favorites:{games:GameResponse[],reviews:ReviewResponse[]},feed:ReviewResponse[]}. Reutiliza as consultas existentes de favoritos/feed e a média local; listas vazias mantêm a estrutura. Query userId não altera identidade.
- GET /community/reviews/recent: público, todas as ReviewResponse[] por createdAt decrescente/ID crescente. Edição e comentários não reposicionam a publicação; inclui autores não seguidos.
- GET /community/reviews/most-commented?period=7d: público, {review:ReviewResponse,commentCount:number,lastCommentAt:UTC}[] com atividade positiva.
- GET /community/games/in-discussion?period=7d: público, {game:GameResponse,commentCount:number,lastCommentAt:UTC}[] com atividade positiva. Soma os comentários de todas as reviews do jogo, uma vez por comentário.

Períodos aceitos exatamente 24h/7d/30d, padrão 7d quando omitido. Valor vazio ou desconhecido: 400. Janelas móveis inclusivas [agora-24/168/720 horas, agora], capturando agora UTC uma vez por consulta e excluindo comentários futuros. Reviews antigas participam por seus comentários recentes. Ordenação dos rankings: commentCount decrescente, lastCommentAt elegível decrescente, ID numérico crescente. Não contar participantes distintos, nota média ou popularidade externa como discussão.

Os resultados são projeções dos dados persistidos, sem tabelas próprias de dashboard/ranking. Catálogo segue cache → PostgreSQL → IGDB. CommentActivity compartilha a seleção temporal dos rankings e ActivityOrdering compartilha seus desempates. ReviewProjection centraliza ordenação e projeção de reviews. Feed usa EXISTS SQL sobre seguimentos. Atividade usa COUNT/MAX por review e janela no SQL. Favoritos de jogos/reviews compartilham snapshot RepeatableRead; dashboard e projeções de catálogo/autores usam consultas separadas, sem snapshot transacional global. A coordenação de publicação em memória foi retirada na P-PR-06.

Compatibilidade por leitura de Front/src/services/communityContracts.ts: ReviewDetails, RankedReview, RankedGame, RankingPeriod e FavoriteLists mantêm a representação. Dashboard agrega as chamadas getFavorites/getFeed numa resposta nomeada. A integração HTTP do frontend permanece fora desta PR.

## Exemplos

```http
GET /api/v1/dashboard
Authorization: Bearer <token>
```

```http
GET /api/v1/feed/following
Authorization: Bearer <token>
```

Consultas públicas:

```http
GET /api/v1/community/reviews/recent
GET /api/v1/community/reviews/most-commented?period=24h
GET /api/v1/community/games/in-discussion?period=30d
```

As fixtures iniciais de comentários são de janeiro/2026. Em outubro/2026, rankings inicialmente vazios são esperados; publicar um comentário em uma review antiga a coloca no ranking aplicável sem promovê-la em Recentes.
