# Mapeamento backend ↔ serviços do frontend

Comparação por leitura de `Front/src/models/index.ts`, `authContracts.ts`, `contracts.ts`, `communityContracts.ts` e `notificationContracts.ts`. O Front continua usando mocks; nenhum adaptador HTTP foi implementado nesta entrega.

| Contrato/serviço TypeScript | HTTP/DTO C# | Adaptação futura |
| --- | --- | --- |
| Id / DateTime | string opaca / DateTimeOffset UTC ISO 8601 | Preservar strings, codificar IDs nas URLs; releaseDate é yyyy-MM-dd |
| User | PublicUserResponse | Mesmos id, username, displayName, bio, avatarUrl; sem email ou credenciais |
| RegistrationInput | POST /auth/register, RegisterRequest | Mantém name/email/password; retorna User com 201, sem sessão automática |
| LoginCredentials / AuthService.login → User | POST /auth/login, LoginResponse | Guardar token/expiresAt e devolver user ao serviço; token não faz parte de User |
| AuthService.logout | POST /auth/logout | Enviar Bearer, tratar 204 e limpar sessão local; API revoga só o token atual |
| Game / GameQuery | GET /games e /games/{id}, GameResponse | name-asc/name-desc; 404 de detalhe vira null no serviço getById; média local, capas das fixtures null |
| Rating / getRating | PUT /games/{id}/rating/me e GET → MyRatingResponse | PUT devolve Rating; GET exige sessão e desembrulha rating, inclusive null |
| Review / PublicReview / ReviewDetails | ReviewResponse | Mantém campos da review e author; game acrescenta coverUrl além de id/title |
| findOwnReview | GET /games/{id}/reviews/me → MyReviewResponse | Desembrulhar review nullable; 404 significa jogo inexistente, não ausência de review própria |
| GameOpinions | GET /games/{id}/opinions | Mesmos averageRating nullable e reviews; zero é média válida |
| PublicComment | GET/POST /reviews/{id}/comments | Mesmos campos e author; sem likes/respostas encadeadas |
| PublicProfile | GET /users/{id} + GET /users/{id}/reviews | Compor {user,reviews}; API não possui um endpoint único desse agregado; edição usa PATCH /users/me/profile |
| Connections | GET /users/{id}/followers + /following | Compor {followers,following}; ambas públicas |
| isFollowing / setFollowing | GET /users/{id}/follow/me e PUT/DELETE /follow | Desembrulhar following; PUT devolve estado, DELETE 204; corpo não permitido |
| Favorite / getFavorite / setFavorite | FavoriteResponse, FavoriteStatusResponse | targetType game/review mantido; GET desembrulha favorite; DELETE 204 vira null; PUT preserva ID na repetição |
| FavoriteLists | GET /favorites | Mesmos games/reviews; dados pessoais dependem do Bearer, não de userId do cliente |
| getFeed | GET /feed/following | ReviewDetails[]; sem sessão a API retorna 401, não uma lista pública substituta |
| Início pessoal | GET /dashboard | Composição {favorites,feed}; catálogo público continua independente para visitantes |
| getRecentReviews | GET /community/reviews/recent | Público, ordem por publicação; edição/comentário não promove review antiga |
| RankedReview / RankedGame / RankingPeriod | Rankings da Comunidade | Mesmos review/game, commentCount e lastCommentAt; 24h/7d/30d, padrão 7d, limites inclusivos UTC |
| NotificationItem | GET /notifications | Mesmos evento e ator público; commentId somente em review-comment, sem campo read. O modelo genérico legado Notification do Front possui read, mas o contrato do serviço não o exige |

## Erros e autorização

ApiError usa código HTTP estável (`validation_failed`, `unauthorized`, `forbidden`, `not_found`, `conflict`, `internal_error`). Os erros dos mocks são mais específicos; o adaptador deverá considerar status e operação:

- Cadastro: 409 → duplicate-email, 400 → invalid-fields.
- Login: 401 → invalid-credentials; payload incompleto → 400.
- Operação privada: 401 → session-required e tratamento da sessão local.
- Editar review: 403 → forbidden; recurso inexistente → 404/not-found.
- Publicar review: 409 → duplicate-review. Texto inválido, nota inválida e self-follow usam 400; mapear conforme a operação, sem tentar inferir regra pelo texto da mensagem.
- Relações/favoritos repetidos são sucesso idempotente; não converter repetição em duplicate-follow ou likes.

Não enviar identidades/autoria em payloads. Requests nomeados rejeitam campos desconhecidos; PUT/DELETE de relações/favoritos não aceitam corpo. PATCH perfil preserva campos omitidos, permite avatarUrl null e rejeita username/email/password/userId. Limites e semântica de trim estão em [AUTH.md](AUTH.md) e [OPINIONS.md](OPINIONS.md).

Sem paginação ou limite silencioso. Fixtures e alterações existem somente em memória; reinício elimina contas criadas, relações, favoritos, opiniões e sessões. Notificações não são recriadas retroativamente. O adaptador IGDB não realiza HTTP externo.
