# Contratos para a terceira fase

Revisão por leitura dos controllers, DTOs, OpenAPI vivo e contratos em Front/src/services e Front/src/models. Nenhum arquivo de Front foi alterado. As adaptações identificadas pertencem ao cliente HTTP descrito em [COMPATIBILITY.md](COMPATIBILITY.md); nenhuma divergência funcional exigiu alterar os DTOs do backend nesta PR.

## Formatos confirmados

- JSON camelCase; IDs são strings opacas, codificadas no path pelo cliente.
- Datas de instantes usam ISO 8601 UTC. O serializador emite offset `+00:00`, também representável por `Z`; o consumidor deve aceitar a representação UTC sem exigir o sufixo Z. `releaseDate` é `yyyy-MM-dd` ou null.
- `GET rating/me` retorna `{rating: null | RatingResponse}`; `GET reviews/me`, `{review: null | ReviewResponse}`; `GET favorites/{kind}/{targetId}`, `{favorite: null | FavoriteResponse}`. Ausência pessoal é 200 com null; recurso inexistente é 404.
- DELETE follow/favorite e POST logout retornam 204 sem bytes JSON. Não chamar parser JSON para esses sucessos.
- Login retorna `{token,expiresAt,user}`; cadastro retorna somente usuário com 201. Token permanece separado do modelo público e somente deve existir na memória da sessão do navegador nesta fase.
- Projeções públicas não contêm senha/hash/email/token. Login é a única resposta que fornece token ao titular. Autor é `{id,displayName,avatarUrl}`. Notificação new-review omite commentId; review-comment inclui commentId. Não existe campo read no contrato do serviço.
- Perfil público é composto de usuário e reviews; conexões são duas consultas públicas. Edição do próprio perfil preserva campos omitidos, aceita avatarUrl null e não permite alterar identidade/autoria.
- Dashboard retorna `{favorites:{games,reviews},feed}`. Catálogo e Comunidade continuam públicos. Feed, favoritos, notas pessoais e notificações exigem Bearer.
- Média null significa ausência; zero é valor válido. Notas aceitam decimais de 0 a 5. Rankings usam 24h/7d/30d, padrão 7d.
- ApiError é `{code,message}`. Status 400/401/403/404/409/500 usam os códigos documentados em [CONTRACTS.md](CONTRACTS.md); mapeamento de erro específico depende da operação, nunca da mensagem.
- PUT/DELETE follow/favorite não recebem corpo. Requests nomeados rejeitam propriedades desconhecidas; não enviar entidades User ou campos de projeção como payload.

## Cobertura reutilizada e complementar

| Verificação | Evidência em tests/Tcc.Api.Tests |
| --- | --- |
| Inventário, métodos, sucesso/erros e acesso por operação | Validation/EndpointInventory.cs e Validation/OpenApiTests.cs |
| Rotas públicas e rejeição de todas as privadas com sessão ausente, falsa, expirada ou revogada | Validation/PermissionMatrixTests.cs |
| Status e comportamentos dos fluxos reais | Identity, Opinions, Social e Feeds; Validation/IntegratedFlowTests.cs |
| Segurança de projeções e erros; normalização de IDs/datas/null | Identity/RegistrationTests.cs, Identity/ProfileTests.cs, Opinions/ReviewQueriesTests.cs, ErrorTests.cs e Validation/OpenApiTests.cs |
| Wrappers raw JSON, casing, datas e corpo zero em DELETE | Validation/ConsumerWireContractTests.cs (novo) |
| Revogação e POST logout sem corpo | Identity/SessionTests.cs |
| CORS e preflight | Validation/CorsTests.cs (novo) |

I-US-02 reutiliza cobertura existente e complementa lacunas do contrato no fio. Não houve divergência que justificasse um RED de correção funcional; não se atribui um RED artificial aos testes de verificação que já passaram na implementação existente.

O inventário abaixo é comparado com o OpenAPI gerado pelos controllers no teste OpenApiTests. Health é diagnóstico; OpenAPI é acessado separadamente em `/openapi/v1.json` com 200 e não se autodocumenta como operação de produto. São 34 operações funcionais mais health.

## Inventário

Métodos, status e acesso seguem abaixo. Erros 401 são comuns a todas as operações privadas; 500 seguro é documentado em todas. A tabela lista os demais erros específicos, incluindo 401 de login inválido.

| Método | Rota | Sucesso | Acesso | Erros específicos |
| --- | --- | --- | --- | --- |
| GET | `/health` | 200 | Público | — |
| POST | `/api/v1/auth/register` | 201 | Público | 400, 409 |
| POST | `/api/v1/auth/login` | 200 | Público | 400, 401 |
| POST | `/api/v1/auth/logout` | 204 | Bearer | — |
| GET | `/api/v1/auth/me` | 200 | Bearer | — |
| GET | `/api/v1/users/{userId}` | 200 | Público | 404 |
| PATCH | `/api/v1/users/me/profile` | 200 | Bearer | 400 |
| GET | `/api/v1/users/{userId}/reviews` | 200 | Público | 404 |
| GET | `/api/v1/users/{userId}/followers` | 200 | Público | 404 |
| GET | `/api/v1/users/{userId}/following` | 200 | Público | 404 |
| GET | `/api/v1/games` | 200 | Público | 400 |
| GET | `/api/v1/games/{gameId}` | 200 | Público | 404 |
| GET | `/api/v1/games/{gameId}/opinions` | 200 | Público | 404 |
| GET | `/api/v1/games/{gameId}/rating/me` | 200 | Bearer | 404 |
| PUT | `/api/v1/games/{gameId}/rating/me` | 200 | Bearer | 400, 404 |
| GET | `/api/v1/games/{gameId}/reviews` | 200 | Público | 404 |
| GET | `/api/v1/games/{gameId}/reviews/me` | 200 | Bearer | 404 |
| POST | `/api/v1/games/{gameId}/reviews` | 201 | Bearer | 400, 404, 409 |
| GET | `/api/v1/reviews/{reviewId}` | 200 | Público | 404 |
| PATCH | `/api/v1/reviews/{reviewId}` | 200 | Bearer | 400, 403, 404 |
| GET | `/api/v1/reviews/{reviewId}/comments` | 200 | Público | 404 |
| POST | `/api/v1/reviews/{reviewId}/comments` | 201 | Bearer | 400, 404 |
| PUT | `/api/v1/users/{userId}/follow` | 200 | Bearer | 400, 404 |
| DELETE | `/api/v1/users/{userId}/follow` | 204 | Bearer | 400, 404 |
| GET | `/api/v1/users/{userId}/follow/me` | 200 | Bearer | 404 |
| GET | `/api/v1/favorites` | 200 | Bearer | — |
| GET | `/api/v1/favorites/{kind}/{targetId}` | 200 | Bearer | 400, 404 |
| PUT | `/api/v1/favorites/{kind}/{targetId}` | 200 | Bearer | 400, 404 |
| DELETE | `/api/v1/favorites/{kind}/{targetId}` | 204 | Bearer | 400, 404 |
| GET | `/api/v1/dashboard` | 200 | Bearer | — |
| GET | `/api/v1/feed/following` | 200 | Bearer | — |
| GET | `/api/v1/community/reviews/recent` | 200 | Público | — |
| GET | `/api/v1/community/reviews/most-commented` | 200 | Público | 400 |
| GET | `/api/v1/community/games/in-discussion` | 200 | Público | 400 |
| GET | `/api/v1/notifications` | 200 | Bearer | — |

## Limites da evidência

Esta entrega comprova contratos HTTP em testes, sem consumo de negócio pela interface. O relatório da terceira fase mantém os endpoints funcionais como PLANEJADO até observação no navegador. Não há SQL, IGDB real ou autenticação de produção. [CORS.md](CORS.md) explica configuração e a validação de navegador ainda pendente.

## Transição P-PR-01

IDs de usuários e referências userId/actorId são JSON number. Usuários, perfis e sessões persistem no PostgreSQL; falha SQL retorna 503/storage_unavailable. Adaptação do Front é dependência de liberação. Detalhes em [POSTGRES.md](POSTGRES.md).
