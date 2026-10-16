# P-PR-05 — Favoritos e seguimentos

Favoritos de jogos, favoritos de reviews e relações de seguir usuários passam a persistir no PostgreSQL. Listas pessoais, rede, feed e dashboard continuam disponíveis após reiniciar a API.

## Funcionalidade e contrato

- PUT /favorites/{kind}/{targetId} mantém sucesso 200 idempotente; repetir preserva o ID e a data de criação. GET retorna {favorite:null} quando o alvo existe e não está salvo. DELETE retorna 204 mesmo quando a associação já foi removida.
- Favoritos são exclusivos da sessão. O mesmo usuário pode salvar jogos e reviews; cada tipo tem sua própria tabela, FK e identidade, sem alvo polimórfico no banco.
- FavoriteResponse mantém id/userId/targetType/targetId, com id e targetId agora numéricos int32. targetType continua game/review. IDs dos favoritos pertencem ao respectivo tipo; um favorito de jogo e um de review podem ter o mesmo número.
- Seguir é direcional, único e idempotente; não é permitido seguir a própria conta. Remover o seguimento não remove favoritos. Listas de seguidores/seguidos continuam públicas e ordenadas por ID numérico.
- Feed seleciona reviews de usuários seguidos, inclusive anteriores ao seguimento. Reviews próprias e de usuários não seguidos não entram. Deixar de seguir retira as publicações do feed.
- Rotas/formatos dos comandos de seguir e os envelopes de listas/feed/dashboard foram preservados. Identidade vem do Bearer; PUT/DELETE não aceitam corpo.

## Código e schema

GameFavorite mapeia jogos_favoritos e ReviewFavorite mapeia reviews_favoritas: Id integer identity, UserId, GameId/ReviewId, CreatedAt UTC, FKs RESTRICT e pares únicos por usuário/alvo. Follow passa a ter CreatedAt e mapeia seguimentos_usuarios com PK composta seguidor/seguido, duas FKs de usuário, CHECK contra auto-seguimento e índice inverso para seguidores.

SocialConfigurations e migration FavoritesAndFollows incluem somente essas três tabelas. Favorite é uma projeção compatível com o DTO genérico; não é uma tabela com FK polimórfica.

IFavorites/IFollows são portas assíncronas com CancellationToken. PostgresFavorites faz upsert parametrizado preservando identidade/data e remoção pelo par. A leitura de favoritos usa duas consultas sob RepeatableRead; reviews favoritas são obtidas por JOIN filtrado pelo titular, sem varrer todas as reviews na aplicação. Projeções de catálogo/autores e médias permanecem em lotes.

PostgresFollows usa INSERT ON CONFLICT DO NOTHING, DELETE e consultas direcionais. FollowingFeed usa EXISTS entre reviews e relações persistidas, sem carregar a rede completa para filtrar em memória. Autores da rede são carregados em lotes de até 500.

InMemoryFavorites/InMemoryFollows foram retirados do runtime e ficam apenas nas fixtures. Nenhum fallback de relações pessoais em memória; falha SQL retorna 503/storage_unavailable. DbContext/repositórios são scoped e nenhum I/O SQL ocorre sob lock síncrono de publicação.

## Notificações e limites

Destinatários de nova review são consultados nos seguimentos SQL antes da transação de publicação. Após receber o ID gerado, a preparação mantém os eventos transitórios e sua visibilidade após commit. Teste SQL confirma destinatário correto e ausência de auto-notificação.

Notificações continuam em memória até P-PR-06; não há garantia durável entre publicação e evento. Os destinatários refletem a leitura anterior à gravação, sem snapshot global entre essa leitura e commit. Dashboard combina consultas SQL separadas; apenas o par de listas de favoritos compartilha seu snapshot RepeatableRead. A próxima PR deve tratar notificações na unidade transacional de publicação.

O Front não foi alterado. Seu consumidor precisa aceitar Favorite.id/targetId numéricos, mantendo targetType para distinguir identidades de tabelas diferentes. Nenhuma relação transitória antiga foi importada automaticamente.

## Validação e operação

RED ab81736 antes da implementação: ausência dos modelos e IDs textuais reproduzidos. 409 testes aprovados na suíte; cenário SQL de feed reforçado e grupo social revalidados em seguida. Smoke IGDB opt-in não executado nesta PR. Build Release/analisadores sem avisos ou erros.

PostgreSQL 17 real exclusivo de testes: materialização, FKs, pares únicos, auto-seguimento, RESTRICT, preservação de ID/CreatedAt, criação concorrente, isolamento, direção, restart, remoção idempotente/null, feed com reviews próprias e não seguidas excluídas, dashboard e destinatários de notificação. Regressões de autorização, body forjado, sessão revogada, falha SQL segura e cache/catálogo preservadas.

Migration aplicada em tcc_pr01_tests e gamesdb, mantendo 5 contas, 42 jogos e as contagens anteriores de reviews/notas/comentários/curtidas. Três tabelas novas; seis migrations no histórico local. Sem seed, limpeza ou migrations no startup. Health, Swagger e rede pública verificados em instância temporária; OpenAPI exportado atualizado. Configurações privadas continuam ignoradas pelo Git.

Branch feat/postgres-pr-05-favoritos-social, P-US-26–31. Entrega local para revisão, sem remoto configurado, publicação ou merge.
