# Catálogo e opiniões — contratos atuais

Base `/api/v1`. Leituras públicas, exceto `/rating/me` e `/reviews/me`. Escritas usam Bearer e identidade da sessão, rejeitando campos desconhecidos nos payloads. IDs locais de usuários/jogos/reviews/avaliações inteiros, datas UTC, null explícito. Comentários ainda têm ID textual. Listagem de jogos paginada desde G-PR-04; listas de opiniões mantêm seus contratos.

GameResponse: id, title, summary, coverUrl nullable, genres, platforms, releaseDate nullable (yyyy-MM-dd), averageRating nullable calculada exclusivamente das notas locais. Listagem usa envelope items/page/pageSize/hasNextPage, term com trim e sort release-desc/name-asc/name-desc (padrão release-desc sem termo, name-asc com termo). Query/ID inválido: 400; ausência confirmada: 404; falha externa: 502/503/504. Consulte [IGDB-CATALOG.md](IGDB-CATALOG.md).

PUT /games/{id}/rating/me recebe {value: number}, finito entre 0 e 5, decimais permitidos. Retorna RatingResponse (id,userId,gameId,value); GET retorna {rating: RatingResponse|null}. Nova nota substitui a anterior preservando ID e par usuário/jogo; média null sem notas e zero é válido.

POST /games/{id}/reviews e PATCH /reviews/{id} recebem apenas {text: string}. Texto trim não vazio, máximo 10.000 caracteres depois do trim. Publicação retorna 201 com Location /api/v1/reviews/{id}; edição 200, preserva ID, userId, gameId, createdAt e atualiza updatedAt. ReviewResponse inclui id,userId,gameId,text,createdAt,updatedAt nullable, author (id,displayName,avatarUrl) e game (id,title,coverUrl). Uma review por usuário/jogo, conflito 409 inclusive concorrência; edição por outro autor 403.

GET /reviews/{id}, /games/{id}/reviews, /users/{id}/reviews e /games/{id}/opinions são públicos. Listas por publicação decrescente, desempate ID numérico crescente. GET /games/{id}/reviews/me retorna {review: ReviewResponse|null}; opiniões retornam {averageRating: number|null,reviews: ReviewResponse[]}.

GET /reviews/{id}/comments retorna lista plana por createdAt crescente/ID ordinal crescente. POST recebe apenas {text: string}, trim não vazio, máximo 2.000 caracteres depois do trim; retorna 201 CommentResponse (id,userId,reviewId,text,createdAt,author). Location aponta a lista consultável de comentários. Permite comentário próprio. Sem parentCommentId, likes ou exclusão nesta etapa. Notificações de publicação/comentário são transitórias até sua PR SQL.

Desde P-PR-03, reviews e avaliações são persistidas no PostgreSQL, incluindo leitura após reinício. Filtros de opiniões e médias são SQL; stores de opiniões em memória ficam apenas nos testes. Publicações usam transação SQL e preparação de eventos antes do commit. Comentários e notificações permanecem em memória. A preparação com falha cancela a review; a entrega durável de eventos depende da PR de notificações. Projeções usam lotes e I/O SQL ocorre fora dos locks síncronos. Consulte P-PR-03.md.

## Exemplo

```http
PUT /api/v1/games/1/rating/me
Authorization: Bearer <token>
Content-Type: application/json

{"value":3.75}
```

```http
POST /api/v1/games/1/reviews
Authorization: Bearer <token>
Content-Type: application/json

{"text":"Minha review sobre o jogo."}
```

Após copiar o ID retornado:

```http
POST /api/v1/reviews/<reviewId>/comments
Authorization: Bearer <token>
Content-Type: application/json

{"text":"Meu comentário."}
```

Consultas seguintes usam `/api/v1/games/1/opinions` e `/api/v1/reviews/<reviewId>/comments`, sem sessão.

Mapeamento das fixtures IGDB de teste: name → title, summary → summary, cover.url → coverUrl, genres.name → genres, platforms.name → platforms, first_release_date → releaseDate UTC sem hora. Os valores são fixtures fictícias, sem HTTP externo, credenciais ou nota externa. Referência: [IGDB](https://api-docs.igdb.com/).

Compatibilidade por leitura de Front: mantém Game, Rating, Review, Comment e PublicReview; o detalhe acrescenta game e os envelopes de ausência exigirão adaptação na futura integração HTTP. Nenhum código de Front integra esta entrega.
