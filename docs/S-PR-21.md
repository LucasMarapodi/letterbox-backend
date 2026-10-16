# S-PR-21 — API unificada de review e nota

Branch `feat/sexta-21-review-nota-api`, base `cb61f08`; RED `3258cdb` confirmado antes do código. Entrega exclusivamente Back. A integração do formulário e consumidores visuais pertence à S-PR-22.

## Contratos

POST `/api/v1/games/{gameId}/reviews` exige Bearer e `{ "rating": 0, "text": null }`. A nota é número JSON finito de 0 a 5, com a precisão double já aceita; zero explícito é válido e ausência/null é erro. Descrição omitida/null/espaços vira `""`; máximo de 10.000 caracteres após trim. Retorna 201 com Location e ReviewResponse incluindo `rating`.

PATCH `/api/v1/reviews/{reviewId}` permite `{ "rating": 4.5, "text": "Descrição" }` e mudanças parciais. Omitir rating/text preserva; text null limpa; rating null é inválido. Só o autor edita. Após qualquer edição válida a review deve ter nota; o autor de um legado sem nota precisa informá-la. IDs, autoria, jogo e data original são preservados.

PUT legado `/api/v1/games/{gameId}/rating/me` mantém `{ "value": 3.25 }` e RatingResponse para compatibilidade, delegando ao mesmo fluxo. Se não existir review, cria uma real sem texto; se existir, altera a nota preservando texto e vínculos. O consumidor novo deve usar POST/PATCH de reviews, não encadear PUT de nota e POST de review: esse segundo POST agora conflita com a review já criada.

ReviewResponse inclui rating em detalhes, consulta própria, opiniões, perfil, favoritos, feed, dashboard e rankings. Nullable somente para legados sem nota, que permanecem legíveis e fora da média até regularização. Descrição vazia não remove a review das listas; ela pode receber comentários, curtidas de comentários e favoritos conforme os endpoints existentes. Likes de reviews da S-PR-08 não são adicionados nesta entrega.

Erros preservados: 400 dados inválidos, 401 sessão inválida/ausente, 403 edição alheia, 404 alvo inexistente, 409 publicação duplicada. Campos desconhecidos/autoria forjada são rejeitados. A escala não foi alterada e não há arredondamento novo.

## Código e consistência

Review referencia opcionalmente sua Evaluation histórica. CreateRated exige nota; EditRated valida a autoria, preserva campos omitidos e exige resolver legados. PublishReview, EditReview e PersonalRating convergem para IReviews.WriteAsync. PostgreSQL bloqueia a linha do autor antes de ler o estado atual, coordenando publicações, PATCH e PUT legado concorrentes. Review, avaliação e notificações de publicação compartilham uma transação; falha reverte todas as escritas. Somente criação publica eventos, edição não os repete.

Avaliacoes continua como única fonte da nota e das médias. Não existe outra coluna de nota em reviews. A navegação Evaluation usa o mesmo par usuário/jogo, carregado por JOIN nas consultas EF, inclusive projeções; não há consulta de nota por item nem store de negócio em memória no runtime. As fixtures adaptadas em memória são exclusivas dos testes.

## Transição

Inventário local anterior: 1 par review+nota, 0 somente nota, 0 somente review e 0 duplicidades em ambas as tabelas. A migration UnifiedReviewRating preserva IDs e conteúdo de reviews existentes; importa somente pares com avaliação sem review, usando descrição vazia e datas da avaliação, sem callbacks/eventos retroativos. Reviews sem avaliação ficam com rating null, sem nota inventada ou conteúdo apagado.

A migration relaxa o CHECK de descrição e adiciona chave alternativa de usuário/jogo em reviews e FK composta RESTRICT de avaliacoes para reviews. Mantém as 16 tabelas e identidades locais integer; nenhuma relação com comentários/favoritos é reatribuída. A FK impede avaliações órfãs inclusive por SQL. A exigência de nota para novas publicações está no domínio/caso de uso: não existe NOT NULL geral na navegação, pois isso eliminaria os legados permitidos. Regularizar essas exceções depende dos autores.

Down remove a nova FK/chave alternativa, preserva os dados importados e restaura o CHECK antigo como NOT VALID para não apagar reviews sem texto. O CHECK passa a valer para novas escritas; downgrade exige coordenação com a aplicação antiga. Não executar Down como procedimento de atualização.

## Verificação e entrega

RED comprovou descrição obrigatória antiga, publicação sem nota aceita, PUT legado sem review e transição ausente. Testes de domínio/HTTP/PostgreSQL cobrem zero, precisão, texto opcional/limites, validação, autoria, unicidade concorrente, edição parcial concorrente, médias, projeções, vínculos, reinício e os três cenários históricos. Falhas de publicação verificam rollback também da avaliação; eventos de edição não são recriados. OpenAPI documenta criação obrigatória, PATCH parcial, nullable legado e compatibilidade do PUT.

Sem alteração no Front. O novo POST é incompatível com o envio antigo somente de texto; S-PR-22 deve enviar rating e text em uma chamada e aceitar rating nullable nas leituras históricas. Sem remoto configurado no Back: revisão e commits locais, sem publicação ou merge.

Validação final: 523 testes aprovados (484 API, 38 Domain, 1 Application), um smoke IGDB live opt-in ignorado; build Release/analisadores com zero avisos e erros. Swagger, health e OpenAPI 200 em instância temporária, encerrada após exportar o documento; 42 operações funcionais e health, sem rota adicional nesta entrega.

Migration aplicada explicitamente em gamesdb após validar host/nome e revisar o inventário. Antes/depois: usuários 7/7, reviews 1/1, avaliações 1/1, comentários 0/0, curtidas de comentários 0/0, reviews favoritas 0/0, notificações 0/0 e migrations 8/9. Não havia pares somente nota ou somente review nesse alvo, nem duplicidades; continuam ausentes. Não houve reset/seed no desenvolvimento. As três situações históricas foram verificadas no alvo exclusivo tcc_pr01_tests, com guarda de host/nome/marcador, incluindo IDs/datas de notas, comentários, curtidas e favoritos preservados.

Reinicie a API habitual para carregar o novo contrato. Não há constraint geral exigindo nota de toda review histórica; os autores precisam resolver eventuais rating null antes de editar. Esta condição não foi mascarada por zero, média ou exclusão de conteúdo. Configurações privadas e exclusão preexistente de .env.example preservadas.
