# Ranking público de reviews mais curtidas

`GET /api/v1/community/reviews/most-liked?period=7d&page=1&pageSize=20` usa `curtidas_reviews`, separado do ranking existente de comentários/respostas.

`period=24h|7d|30d` (padrão 7d), `page>=1` e `pageSize=10|20|50` (padrão 20). Valores inválidos e overflow do offset retornam 400. A janela UTC é inclusiva e considera a data das curtidas ativas, incluindo reviews antigas; curtidas futuras não entram no período. Ordenação no PostgreSQL: quantidade no período DESC, última curtida elegível DESC e ID da review ASC, antes de OFFSET/LIMIT.

Envelope `{items,page,pageSize,hasNextPage}`; cada item contém `{review,periodLikeCount,lastLikeAt}`. `review` reutiliza ReviewResponse completo, incluindo autor, jogo/capa, título nullable, texto, nota e likeCount total ativo, independente do período. A consulta usa uma projeção SQL com joins e agregados; não chama IGDB. Público/no-store, sem likedByMe ou dados privados. Unlike é refletido na próxima consulta. Banco indisponível retorna 503 seguro. Nenhuma migration necessária.

Validação: sete regressões novas reproduziram 404 antes da implementação. Suíte do backend: 588 aprovados e um smoke IGDB opt-in ignorado (533 API, 54 Domain, uma Application). PostgreSQL exclusivo cobre reviews antigas, limites inclusivos de período, curtida futura, quantidade total versus período, nota zero/título/autor/jogo, desempates, 21 reviews/paginação, ausência de dados privados e remoção de curtidas. Build Release/analisadores sem avisos ou erros; health/Swagger/OpenAPI 200 e documento exportado atualizado.

Banco de desenvolvimento preservado; testes SQL executados apenas no alvo exclusivo identificado pelo harness.
