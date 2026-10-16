# S-PR-10 — Concluir o contrato de likes de comentários

Branch `feat/sexta-10-comentario-likes-contrato`, base `cdbe2db` (`main`). Pré-requisito P-US-22–25 atendido pela P-PR-04, publicada retrospectivamente na [PR #17](https://github.com/LucasMarapodi/letterbox-backend/pull/17). Commit RED `4c91f9f` anterior ao código: quatro falhas funcionais e três cenários de visitante aprovados.

## Funcionalidade e contrato

PUT e DELETE `/api/v1/comments/{commentId}/like` exigem Bearer, não aceitam corpo e retornam 204 sem conteúdo. PUT repetido ou concorrente mantém um único vínculo e sua data original. DELETE repetido de comentário existente tem sucesso. Principais, respostas e curtida no próprio comentário usam as mesmas rotas e regras. A identidade vem da sessão, nunca de um campo enviado pelo cliente.

GET `/api/v1/comments/{commentId}/like/me` retorna somente `{ "likedByMe": true }` ou false, com `Cache-Control: no-store, no-cache`. Ausência de curtida é 200/false; ausência de comentário é 404. IDs inválidos (inclusive texto e overflow de int32) retornam 400; visitante recebe 401. Falha SQL preserva o tratamento existente 503/storage_unavailable. Os três endpoints são autenticados e não compartilham cache pessoal.

GET `/api/v1/reviews/{reviewId}/comments` continua público e plano, ordenado por criação/ID, incluindo `parentCommentId` e `likeCount` inteiro não negativo. A lista é igual para visitante/autenticado e não contém `likedByMe`. Likes não geram notificações nem alteram favoritos ou likes de reviews.

**Impacto no consumidor:** o campo privado anterior `liked` foi substituído por `likedByMe`, conforme S-US-10. O contrato público da lista não mudou. O Front não foi alterado nesta entrega; seu consumo visual pertence à S-PR-11. Reinicie a API local para carregar o novo assembly/contrato.

## Código e persistência

`CommentLikeStatusResponse` passa a declarar `LikedByMe`. `CommentLikesController` aplica a política `ResponseCache` com `NoStore=true` e `Location=None`. O OpenAPI documenta o estado privado e o mínimo zero de `CommentResponse.likeCount`; o documento exportado foi atualizado a partir da API compilada.

Reutilizados `CommentLike`, `ICommentLikes`, `PersonalCommentLike`, `PostgresCommentLikes` e a tabela `curtidas_comentarios`, com PK composta integer usuário/comentário e FKs RESTRICT. INSERT ON CONFLICT DO NOTHING e DELETE pelo par permanecem a fonte SQL da idempotência. `PostgresComments.ForReviewAsync` já retorna contagens SQL na consulta da lista, com autores consultados em lote. Nenhuma entidade, tabela, API paralela, migration ou contador persistido foi adicionado.

## Validação

O lote direcionado passou com 16 testes, incluindo P-PR-04 e fluxo final de persistência. Os novos cenários HTTP/PostgreSQL real validam principais/respostas, curtida própria, dois usuários, oito PUT/DELETE concorrentes por alvo, data original preservada, reinício, remoção e contagem, ausência de eventos/favoritos, rejeição de payload/autoria forjada e IDs inválidos/ausentes. Um interceptor comprova uma única consulta contendo contagens para 12 comentários, sem registrar parâmetros; as respostas públicas não contêm estado pessoal.

Verificação final: **556 testes aprovados** (503 API, 52 Domain e um Application), somente o smoke live IGDB opt-in ignorado. Build Release com analisadores: zero avisos e erros. `git diff --check` aprovado.

Health, Swagger e OpenAPI responderam 200 numa API temporária em porta livre, encerrada após a exportação. O schema publicado e o retorno HTTP foram verificados com `likedByMe` booleano e contagem integer/int32 com mínimo zero. Testes usam apenas `tcc_pr01_tests`, com a guarda existente de host/nome/marcador; nenhum reset, seed ou migration foi executado em `gamesdb`.

Os binários de validação usam `-p:OutputPath=bin/SPr10/`: a primeira tentativa na saída padrão encontrou DLLs bloqueadas pela API local. Esse erro de infraestrutura não foi usado como RED. A API habitual não foi encerrada para executar os testes.
