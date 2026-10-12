# Catálogo e opiniões — PR-3

Base `/api/v1`. Leituras públicas, exceto `/rating/me` e `/reviews/me`. Escritas usam Bearer e identidade da sessão, rejeitando campos desconhecidos nos payloads. IDs opacos, datas UTC, null explícito. Sem paginação ou limites silenciosos de quantidade.

GameResponse: id, title, summary, coverUrl nullable, genres, platforms, releaseDate nullable (yyyy-MM-dd), averageRating nullable calculada exclusivamente das notas locais. term é trim e busca parcial sem diferenciar maiúsculas; sort padrão name-asc, aceita name-asc/name-desc. Desempate por ID ordinal. Sort inválido: 400; recurso ausente: 404.

PUT /games/{id}/rating/me recebe {value: number}, finito entre 0 e 5, decimais permitidos. Retorna RatingResponse (id,userId,gameId,value); GET retorna {rating: RatingResponse|null}. Nova nota substitui a anterior preservando ID e par usuário/jogo; média null sem notas e zero é válido.

POST /games/{id}/reviews e PATCH /reviews/{id} recebem apenas {text: string}. Texto trim não vazio, máximo 10.000 caracteres depois do trim. Publicação retorna 201 com Location /api/v1/reviews/{id}; edição 200, preserva ID, userId, gameId, createdAt e atualiza updatedAt. ReviewResponse inclui id,userId,gameId,text,createdAt,updatedAt nullable, author (id,displayName,avatarUrl) e game (id,title,coverUrl). Uma review por usuário/jogo, conflito 409 inclusive concorrência; edição por outro autor 403.

GET /reviews/{id}, /games/{id}/reviews, /users/{id}/reviews e /games/{id}/opinions são públicos. Listas por publicação decrescente, desempate ID ordinal crescente. GET /games/{id}/reviews/me retorna {review: ReviewResponse|null}; opiniões retornam {averageRating: number|null,reviews: ReviewResponse[]}.

GET /reviews/{id}/comments retorna lista plana por createdAt crescente/ID ordinal crescente. POST recebe apenas {text: string}, trim não vazio, máximo 2.000 caracteres depois do trim; retorna 201 CommentResponse (id,userId,reviewId,text,createdAt,author). Location aponta a lista consultável de comentários. Permite comentário próprio. Sem parentCommentId, likes, exclusão ou eventos/notificações nesta PR.

Fixtures: três jogos fictícios correspondentes aos IDs/títulos do Front, capas null nesta entrega; sem notas iniciais. Review review-fixture de Ana em game-3; três comentários de Ana nessa review (comment-c de 01/01/2026 e comment-a/comment-b de 02/01/2026, às 12h UTC). Cada repositório singleton inicializa suas fixtures uma vez, sem usar a marca global de seed da identidade para impedir sementes de outras áreas. Reinício restaura dados iniciais e elimina gravações.

## Exemplo

```http
PUT /api/v1/games/game-1/rating/me
Authorization: Bearer <token>
Content-Type: application/json

{"value":3.75}
```

```http
POST /api/v1/games/game-1/reviews
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

Consultas seguintes usam `/api/v1/games/game-1/opinions` e `/api/v1/reviews/<reviewId>/comments`, sem sessão.

Mapeamento IGDB fake: name → title, summary → summary, cover.url → coverUrl, genres.name → genres, platforms.name → platforms, first_release_date → releaseDate UTC sem hora. Os valores são fixtures fictícias, sem HTTP externo, credenciais ou nota externa. Referência: [IGDB](https://api-docs.igdb.com/).

Compatibilidade por leitura de Front: mantém Game, Rating, Review, Comment e PublicReview; o detalhe acrescenta game e os envelopes de ausência exigirão adaptação na futura integração HTTP. Nenhum código de Front integra esta entrega.
