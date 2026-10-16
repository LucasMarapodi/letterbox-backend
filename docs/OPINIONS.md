# Catálogo e opiniões — contratos atuais

S-PR-08: PUT/DELETE `/api/v1/reviews/{reviewId}/like`, autenticados e sem corpo, retornam 204 e são idempotentes. GET `/api/v1/reviews/{reviewId}/like/me` retorna `{likedByMe:boolean}` privado/no-store. ReviewResponse inclui likeCount inteiro agregado em SQL em todas as projeções, sem estado personalizado. Curtir a própria review é permitido; não altera favoritos nem gera notificações. [Contrato e persistência](S-PR-08.md).

Base `/api/v1`. Leituras públicas, exceto `/rating/me`, `/reviews/me` e `/comments/{id}/like/me`. Escritas usam Bearer e identidade da sessão, rejeitando campos desconhecidos nos payloads. IDs locais de usuários/jogos/reviews/avaliações inteiros, datas UTC, null explícito. Comentários também usam ID inteiro desde P-PR-04. Listagem de jogos paginada desde G-PR-04; listas de opiniões mantêm seus contratos.

GameResponse: id, title, summary, coverUrl nullable, genres, platforms, releaseDate nullable (yyyy-MM-dd), averageRating nullable calculada exclusivamente das notas locais. Listagem usa envelope items/page/pageSize/hasNextPage, term com trim e sort release-desc/name-asc/name-desc (padrão release-desc sem termo, name-asc com termo). Query/ID inválido: 400; ausência confirmada: 404; falha externa: 502/503/504. Consulte [IGDB-CATALOG.md](IGDB-CATALOG.md).

PUT /games/{id}/rating/me recebe {value: number}, finito entre 0 e 5, decimais permitidos. Retorna RatingResponse (id,userId,gameId,value); GET retorna {rating: RatingResponse|null}. Desde S-PR-21, delega ao fluxo unificado: cria review sem texto quando ausente ou atualiza a nota da existente preservando texto/ID/vínculos. Não cria nota órfã. Média null sem notas e zero é válido.

POST /games/{id}/reviews recebe {rating: number obrigatório, text?: string|null}. Nota finita de 0 a 5, zero válido; texto omitido/null/espaços vira vazio, até 10.000 caracteres após trim. PATCH /reviews/{id} preserva campos omitidos, text null limpa e rating null é inválido; legado sem nota exige informá-la. Publicação retorna 201 com Location /api/v1/reviews/{id}; edição 200, preserva ID, userId, gameId, createdAt e vínculos, atualizando updatedAt. ReviewResponse inclui id,userId,gameId,text,rating,createdAt,updatedAt nullable, author (id,displayName,avatarUrl) e game (id,title,coverUrl). rating é nullable somente para histórico sem nota, preservado até regularização pelo autor. Uma review por usuário/jogo, conflito 409 inclusive concorrência; edição por outro autor 403. Perfil, feed, favoritos e rankings incluem a nota. [Transição e validações S-PR-21](S-PR-21.md).

GET /reviews/{id}, /games/{id}/reviews, /users/{id}/reviews e /games/{id}/opinions são públicos. Listas por publicação decrescente, desempate ID numérico crescente. GET /games/{id}/reviews/me retorna {review: ReviewResponse|null}; opiniões retornam {averageRating: number|null,reviews: ReviewResponse[]}.

GET /reviews/{id}/comments retorna lista plana por createdAt/id numérico crescentes. POST recebe text e parentCommentId opcional/null: principal ou resposta a principal da mesma review, sem segundo nível. Texto trim não vazio até 2.000 caracteres. Retorna 201 CommentResponse (id,userId,reviewId,text,createdAt,author,parentCommentId,likeCount). Location aponta a lista. PUT/DELETE /comments/{id}/like autenticados, sem body, retornam 204 idempotente; GET /comments/{id}/like/me retorna {likedByMe:boolean}, privado/no-store. Curtida própria e em resposta permitidas; contagem pública SQL, sem notificações de like. S-PR-10 substitui o campo anterior liked por likedByMe; o consumidor deve acompanhar o novo nome. Consulte [S-PR-10.md](S-PR-10.md) e P-PR-04.md.

Desde P-PR-03, reviews e avaliações são persistidas no PostgreSQL, incluindo leitura após reinício. Filtros de opiniões e médias são SQL; stores de opiniões em memória ficam apenas nos testes. Publicações usam transação SQL e preparação de eventos antes do commit. Comentários e curtidas também usam PostgreSQL desde P-PR-04; notificações também persistem desde P-PR-06 na mesma transação da review/comentário. Falha na gravação de eventos cancela a publicação e nenhum registro fica visível antes do commit. Projeções usam lotes e I/O SQL ocorre fora dos locks síncronos. Consulte P-PR-03.md.

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
