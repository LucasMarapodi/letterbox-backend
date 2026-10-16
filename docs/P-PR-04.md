# P-PR-04 — Comentários, respostas e curtidas

Comentários e respostas passam a persistir no PostgreSQL. A API permite responder a um comentário principal da mesma review e curtir comentários ou respostas. Reiniciar a API preserva a discussão e as curtidas.

## Funcionalidade e contratos

- POST /api/v1/reviews/{reviewId}/comments recebe text e parentCommentId opcional. Omitir o pai ou enviar null cria comentário principal; informar um inteiro positivo cria resposta. Autor/data vêm da sessão/relógio.
- Texto com trim obrigatório, não vazio, até 2.000 caracteres. Pai ausente retorna 404; pai de outra review, resposta a resposta ou pai inválido retorna 400. Campos de autoria não são aceitos no payload.
- GET na mesma rota retorna lista plana por criado_em/id crescentes. CommentResponse inclui id numérico, userId, reviewId, text, createdAt, author, parentCommentId nullable e likeCount público.
- PUT e DELETE /api/v1/comments/{commentId}/like exigem Bearer e nenhum corpo; retornam 204 e são idempotentes, inclusive em concorrência. É permitido curtir o próprio comentário e respostas.
- GET /api/v1/comments/{commentId}/like/me exige Bearer e retorna {liked: boolean}, exclusivamente para a sessão atual. Alvo inexistente retorna 404 e ID inválido retorna 400.
- Curtidas não geram notificações. Comentários/respostas mantêm a notificação existente para o autor da review, exceto auto-notificação.
- Comment.id, ParentCommentId e referências commentId nas notificações são inteiros. IDs próprios de notificações/favoritos ainda seguem a transição das próximas PRs.

## Código e integridade

Comment tem ID identity, UserId/ReviewId e ParentCommentId opcional, texto e data UTC. Comment.Reply valida pai persistido, principal e da mesma review. CommentLike usa somente UserId/CommentId/CreatedAt, com chave composta e sem contador manual.

CommentConfigurations e migration CommentsAndLikes criam comentarios e curtidas_comentarios. FKs RESTRICT, texto não vazio/limite 2.000, CHECK contra autopai, chave alternativa review_id/id e FK composta review_id/comentario_pai_id garantem pertencimento. Trigger PostgreSQL exige pai principal. Review/pai/identidade de comentário publicado são imutáveis, impedindo reparenting concorrente, ciclos e segundo nível inclusive por SQL direto. Não há endpoint de edição, movimentação ou exclusão de comentários.

PostgresComments implementa IComments assíncrono. Publicação usa transação e obtém o ID antes de preparar notificações; falha na preparação cancela comentário e eventos pendentes. Eventos transitórios só ficam visíveis após o commit. I/O SQL ocorre fora dos locks síncronos de publicação.

PostgresCommentLikes implementa ICommentLikes com INSERT ON CONFLICT DO NOTHING e DELETE por par. Listagem obtém contagens em uma consulta SQL; rankings agregam COUNT/MAX por review e intervalo no SQL, incluindo respostas e excluindo comentários futuros. Autores são projetados em lotes. InMemoryComments foi retirado do runtime e está somente no assembly de testes; não existe fallback de comentários/curtidas em memória no produto.

## Validação e operação

RED 02995f7 antes da implementação: ausência do modelo relacional e ID textual reproduzidos. 405 testes validados: suíte de 403, seguida dos dois novos cenários e regressões do grupo. PostgreSQL 17 real exclusivo de testes cobre materialização, restart, respostas, cross-review, segundo nível/ciclos, texto/FKs/RESTRICT, unicidade e concorrência de likes, isolamento por usuário, contagem pública, payload indevido, rollback de preparação de notificações e rankings SQL. Downgrade/upgrade da nova migration foi exercitado somente no banco exclusivo, preservando conta/review anteriores.

Build Release com analisadores sem avisos/erros. Smoke IGDB opt-in não foi executado nesta PR. Health, Swagger, OpenAPI e ranking público verificados em instância temporária local; documento exportado atualizado.

Migration aplicada em tcc_pr01_tests e gamesdb. Upgrade de desenvolvimento preservou 5 contas e 42 jogos; reviews/avaliações existentes permaneceram com as contagens anteriores. Duas tabelas adicionadas, cinco migrations no histórico. Nenhuma limpeza, seed ou migration automática no startup. Configuração privada permanece ignorada pelo Git.

## Limites e entrega

Front não foi alterado. Para compatibilidade, seu consumidor precisa aceitar Comment.id/parentCommentId/commentId numéricos e os novos campos da lista. A interface de respostas/curtidas não faz parte desta PR.

Favoritos, seguimentos e notificações continuam em memória até suas etapas. Notificações ainda não são duráveis: há janela entre commit SQL e publicação do evento transitório. A futura PR de notificações deve persistir evento e comentário na mesma transação.

Branch feat/postgres-pr-04-comentarios-likes, P-US-22–25. Entrega local sem remoto configurado; sem publicação ou merge.
