# P-PR-06 — Notificações persistentes

Notificações passam a persistir no PostgreSQL junto da review ou comentário que as originou. Uma falha após inserir eventos cancela também a publicação; nenhuma outra requisição enxerga os registros antes do commit. Reiniciar a API preserva os eventos.

## Funcionalidade e contrato

- Nova review notifica os seguidores consultados no SQL dentro da transação de publicação.
- Comentário/resposta notifica somente o autor da review, quando diferente do ator. Responder não adiciona novos destinatários.
- Não há auto-notificação, eventos por edição/curtida, marcação de leitura ou envio manual.
- GET /api/v1/notifications mantém Bearer obrigatório e lista exclusiva do titular. userId na query não muda o destinatário. Ordenação SQL: criado_em decrescente, id numérico crescente no empate.
- NotificationResponse mantém os campos existentes, com id agora inteiro int32. commentId é numérico e continua omitido em new-review. Dados do ator são públicos, projetados em lote.
- Repetir o mesmo evento/destinatário não cria outro registro nem altera identidade/data. Não são criadas notificações retroativas para publicações antigas.

## Código e schema

NotificationConfiguration e migration PersistentNotifications adicionam notificacoes: id integer identity, destinatario_id/ator_id/review_id obrigatórios, comentario_id opcional, tipo restrito e criado_em UTC. Todas as FKs usam RESTRICT. A FK composta review_id/comentario_id garante que o comentário pertence à review indicada.

CHECKs impedem auto-notificação e incoerência entre tipo e presença de comentário. Dois índices únicos parciais tratam eventos com null corretamente: destinatário/review para new-review e destinatário/comentário para review-comment. Índice destinatário/data/id atende à leitura privada.

INotifications é assíncrono, com CancellationToken. PostgresNotifications usa INSERT parametrizado por lotes de até 500 com unnest e ON CONFLICT DO NOTHING. O writer exige transação ativa; a leitura filtra o destinatário e ordena no SQL, com AsNoTracking.

IReviews/IComments recebem callback assíncrono executado após obter o ID gerado e antes do commit. PublicationNotifications resolve seguidores e grava eventos usando o mesmo DbContext scoped e a mesma transação da publicação. IGDB, catálogo e projeções necessárias são resolvidos antes de iniciar essa transação. Não há coordenação por lock síncrono, dual write ou fallback de notificações em memória.

InMemoryNotifications/InMemoryPublicationScope e o contrato de coordenação em memória foram retirados do produto; existem somente nas fixtures de testes. Todos os modelos de negócio da fase têm fonte SQL. Cache de catálogo/snapshots e token Twitch continuam transitórios por finalidade própria.

## Validação e operação

RED 73ab147 antes da implementação: ausência do modelo e ID textual reproduzidos. 414 testes aprovados na suíte completa; smoke IGDB opt-in não executado nesta PR. Build Release com analisadores sem avisos/erros.

PostgreSQL 17 real exclusivo de testes verifica concorrência de reviews sem eventos duplicados, restart de eventos de review/comentário, isolamento de destinatário, auto-notificação suprimida, repetição preservando IDs/datas, constraints de tipo, unicidade com comentário null e FK composta. Writer real com falha injetada após INSERT comprova rollback de review/comentário e seus eventos; writer pausado antes do commit comprova invisibilidade para outras requisições. Fixtures anteriores de falha SQL foram adaptadas para compartilhar o contexto transacional real.

Migration aplicada em tcc_pr01_tests e gamesdb. Upgrade local preservou 6 contas, 43 jogos, 1 review, 2 favoritos de jogos e as demais contagens verificadas; sete migrations no histórico. Não houve seed, importação de eventos antigos, limpeza ou migration no startup. Health, Swagger, OpenAPI numérico e resposta 401 do endpoint sem sessão conferidos em instância temporária. Configurações privadas permanecem ignoradas pelo Git.

## Compatibilidade e entrega

Front não alterado. O consumidor deve aceitar Notification.id como inteiro e desempatar IDs numericamente; demais campos/envelope permanecem. Reiniciar a API carrega a nova implementação.

A P-PR-07 permanece pendente para a validação final do conjunto; esta entrega não declara toda a quinta fase encerrada. Branch feat/postgres-pr-06-notificacoes, P-US-32–33. Entrega local sem remoto configurado; sem publicação ou merge.
