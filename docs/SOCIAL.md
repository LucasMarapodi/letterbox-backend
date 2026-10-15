# Interações sociais e notificações — contratos atuais

Base `/api/v1`. Identidade pessoal resolvida pelo Bearer. IDs locais numéricos int32 e datas UTC. Erros seguem ApiError. Comandos de seguir/favoritar não aceitam corpo; consultas pessoais usam exclusivamente a sessão.

PUT /users/{userId}/follow retorna 200 {following:true}; DELETE retorna 204, inclusive repetido. GET /users/{userId}/follow/me retorna {following:boolean}. Alvo inexistente: 404. Seguir a própria conta: 400. GET /users/{userId}/followers e /following são públicos, retornam PublicUserResponse[] por ID numérico crescente. PUT/DELETE/estado pessoal exigem sessão. Relações direcionais persistem em seguimentos_usuarios, com PK composta, FKs RESTRICT, CreatedAt e CHECK sem auto-seguimento.

FavoriteKind aceita game/review. GET /favorites retorna {games:GameResponse[],reviews:ReviewResponse[]} por ID numérico crescente. GET /favorites/{kind}/{targetId} retorna {favorite:FavoriteResponse|null}; PUT retorna FavoriteResponse (id,userId,targetType,targetId), preservando ID/data na repetição; DELETE retorna 204. Todos privados. IDs devem ser inteiros positivos; tipo inválido: 400; alvo inexistente: 404, inclusive em consulta/remoção.

JogosFavoritos e ReviewsFavoritas usam tabelas separadas, FKs reais e pares únicos por usuário/alvo. Favorite.id e targetId são numéricos; targetType identifica o tipo e distingue IDs de tabelas diferentes. O mesmo número de favorito pode existir nos dois tipos. Não há favoritos públicos de terceiros. Curtidas de comentários são relações independentes; consulte OPINIONS.md.

Feed usa EXISTS SQL sobre as relações do titular; listas de reviews favoritas usam JOIN filtrado no SQL. Os dois grupos de favoritos compartilham snapshot RepeatableRead, com projeções de catálogo/autores em lotes. Reiniciar o host preserva favoritos, rede e feed. Detalhes em [P-PR-05.md](P-PR-05.md).

Notificações: nova review notifica os seguidores observados na leitura SQL anterior à publicação; comentário/resposta notifica o autor da review se for outra pessoa. Edição e curtidas não notificam. Tipos: new-review/review-comment. Deduplicação por tipo/evento/destinatário. Destinatários são consultados fora dos locks síncronos, antes de iniciar a transação de publicação.

Review/comentário recebe ID no SQL antes de preparar eventos. Falha na preparação cancela a transação e eventos pendentes; eventos em memória tornam-se visíveis após commit. Notificações continuam transitórias até P-PR-06: não há durabilidade garantida entre commit SQL e publicação em memória, nem snapshot global com alterações concorrentes de seguimento. Novo host inicia somente as notificações vazias, sem gerar eventos retroativos.

GET /notifications é privado e retorna eventos do titular por createdAt decrescente e ID textual ordinal crescente. DTO: id textual, userId/actorId/reviewId numéricos, createdAt, type, actor (id,displayName,avatarUrl); commentId numérico apenas para review-comment. Sem campo read, envio manual ou e-mail.

O Front não foi alterado. Tipos/decoders devem aceitar Favorite.id/targetId numéricos, além dos IDs e referências migrados nas PRs anteriores. Tokens opacos permanecem strings de autenticação.

Exemplos sem corpo: PUT /api/v1/users/1/follow e PUT /api/v1/favorites/game/1, com Authorization: Bearer <token>. Em ambos, a identidade do titular vem da sessão.
