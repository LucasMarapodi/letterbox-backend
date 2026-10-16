# S-PR-08 — Persistir likes de reviews e expor endpoints

Branch `feat/sexta-08-review-likes-api`, base `b2e8601`. Commit RED `fa45127` anterior ao código, com falhas funcionais pela ausência da entidade, tabela, rotas e contagens. Escopo exclusivamente Back; integração visual na S-PR-09.

## Funcionalidade

PUT e DELETE `/api/v1/reviews/{reviewId}/like` exigem Bearer, não aceitam corpo e retornam 204 sem conteúdo. PUT repetido/concorrente mantém somente um vínculo e preserva a data original; DELETE repetido de review existente também tem sucesso. O autor pode curtir sua própria review. O usuário vem exclusivamente da sessão; não há userId no contrato.

GET `/api/v1/reviews/{reviewId}/like/me` retorna `{ "likedByMe": true }` ou false para o usuário autenticado, com Cache-Control no-store. Ausência de curtida é 200/false; ausência de review é 404. Todos os três endpoints retornam 400 para ID inválido, 401 para sessão inválida/ausente, 404 para review inexistente e 503 para persistência indisponível. PUT/DELETE com corpo, inclusive tentativa de informar autoria, retornam 400.

ReviewResponse acrescenta `likeCount` integer int32 não negativo em detalhes, consulta própria, opiniões/jogos, perfil, favoritos, feed, dashboard e rankings de reviews da Comunidade. Reviews sem likes retornam zero. As projeções não incluem likedByMe; personalização é consultada separadamente pelo endpoint privado.

Likes de reviews são uma nova relação autorizada na sexta fase. Favoritar continua uma ação independente: curtidas não criam/removem favoritos e favoritos não entram no contador. Nenhuma notificação é gerada por like/unlike. Reviews somente com nota e reviews legadas continuam compatíveis com o contrato de S-PR-21.

## Código e banco

Domain adiciona ReviewLike com UserId/ReviewId positivos e CreatedAt normalizado para UTC. Application adiciona IReviewLikes e PersonalReviewLike, validando sessão e existência da review sem resolver catálogo externo. Api registra os serviços SQL e expõe controller/DTO de status tipados.

PostgresReviewLikes usa INSERT parametrizado com ON CONFLICT DO NOTHING; DELETE filtra o par do usuário atual. CountsAsync agrega por review em SQL com GROUP BY/COUNT. OpinionViews coleta IDs distintos e consulta em lotes de até 500, sem consulta por item ou contador gravado. Dashboard combina IDs dos favoritos/feed no mesmo lote. Toda projeção existente recebe a mesma contagem, sem buscar o estado de outro usuário. A fixture em memória está somente no assembly de testes; runtime persiste exclusivamente em PostgreSQL.

Migration `20261016151043_ReviewLikes` cria somente `curtidas_reviews`: usuario_id integer obrigatório, review_id integer obrigatório e criado_em timestamp with time zone obrigatório; PK composta usuario_id/review_id, FKs RESTRICT para usuarios/reviews e índice por review_id. Um usuário tem zero ou muitos likes e uma review recebe zero ou muitos likes; cada par existe no máximo uma vez. Nenhuma identidade local muda e não há coluna de contador ou migração de favoritos.

Up não altera tabelas/dados existentes. Down remove somente a nova tabela e seus likes; não é parte do upgrade normal. Snapshot/designer preservam o ownership RESTRICT existente. A auditoria passa a conferir 17 tabelas e dez migrations no alvo exclusivo de testes.

## Evidências

Testes HTTP/PostgreSQL real isolado verificam autenticação, IDs inválidos/inexistentes, recusa de corpo, própria review, doze PUTs concorrentes, idempotência/data original, reinício, isolamento entre dois usuários, remoção/contagem, favoritos independentes e ausência de notificações. Detalhes, perfil, jogos, consulta própria, feed, dashboard, favoritos e rankings foram conferidos com contagens 0/1/2 e sem estado personalizado. Interceptor SQL comprova uma única consulta agrupada de likes para a lista de três reviews; não registra valores de parâmetros.

Matriz de permissões e OpenAPI incluem os três endpoints. Contratos documentam body ausente, 204, autorização, status booleano privado e likeCount integer mínimo zero. O smoke IGDB live continua opt-in; não é evidência de chamada externa nesta entrega.

Sem arquivos Front alterados. Remoto do Back não configurado: commits e revisão locais, sem publicação/merge. Configurações privadas e exclusão preexistente de .env.example preservadas.

Verificação final: 532 testes aprovados (492 API, 39 Domain, 1 Application), apenas smoke IGDB live opt-in ignorado. Build Release/analisadores com zero avisos e erros. Instância temporária respondeu health/Swagger/OpenAPI 200 e foi encerrada após exportar o contrato com 45 operações funcionais e health.

Migration aplicada explicitamente no gamesdb local após validar o alvo e revisar o SQL. Antes/depois: usuários 11/11, reviews 4/4, notas 4/4, comentários 0/0, reviews favoritas 3/3, notificações 0/0 e migrations 9/10. A nova tabela ficou com zero likes e PK/FKs conferidas. Não houve seed, reset ou conversão de favoritos no desenvolvimento; escritas sintéticas ficaram no banco exclusivo tcc_pr01_tests. Reinicie a API habitual para carregar as novas rotas.
