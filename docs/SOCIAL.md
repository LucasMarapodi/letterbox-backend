# Interações sociais e notificações — PR-4

Base `/api/v1`. Identidade pessoal sempre resolvida pelo Bearer; IDs opacos e datas UTC. Erros seguem ApiError. Sem parâmetros de identidade no corpo; comandos de seguir/favoritar não aceitam corpo. Consultas pessoais ignoram userId em query e retornam apenas dados do token.

PUT /users/{userId}/follow retorna 200 {following:true}; DELETE retorna 204, inclusive repetido. GET /users/{userId}/follow/me retorna {following:boolean}. Alvo inexistente: 404. Seguir a própria conta: 400. GET /users/{userId}/followers e /following são públicos e retornam PublicUserResponse[], por ID ordinal crescente. PUT/DELETE/estado pessoal exigem sessão. Unicidade follower/followed protegida contra concorrência.

FavoriteKind aceita exatamente game/review. GET /favorites retorna {games:GameResponse[],reviews:ReviewResponse[]}, por ID crescente, sem limites de quantidade. GET /favorites/{kind}/{targetId} retorna {favorite:FavoriteResponse|null}; PUT retorna FavoriteResponse (id,userId,targetType,targetId), preservando ID na repetição; DELETE retorna 204. Todos privados. Tipos inválidos: 400; alvo ausente: 404, também em consulta/remoção. Unicidade usuário/tipo/alvo sob concorrência. Sem favoritos públicos de terceiros ou likes.

Notificações: nova review notifica seguidores atuais do autor; comentário notifica autor da review se for outra pessoa. Edição/falhas não notificam. NotificationType: new-review/review-comment. Evento identifica destinatário, ator, review, data da publicação, commentId quando aplicável. Deduplicação por tipo/evento/destinatário. Publicação e geração são síncronas em memória; leitores não observam uma publicação com notificações parcialmente geradas.

GET /notifications é privado e retorna todas as notificações do token, por createdAt decrescente e ID ordinal crescente. DTO: id,userId,actorId,reviewId,createdAt,type,actor (id,displayName,avatarUrl); commentId apenas para review-comment. reviewId permite montar o link para a review. Não há campo read nem operações de leitura/publicação manual/e-mail. Nenhuma fixture de notificações é criada retroativamente a partir das reviews/comentários anteriores. Novo host inicia relações, favoritos e notificações vazios.

Compatibilidade por leitura: Connections/User, FavoriteLists e NotificationItem dos serviços de Front preservados; envelopes de estado pessoal e armazenamento Bearer serão tratados na integração HTTP futura. NotificationItem não exige read (apesar do modelo genérico legado Notification ter esse campo). Front permanece inalterado.
