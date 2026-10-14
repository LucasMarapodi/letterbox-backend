# G-PR-04 — API paginada e regras locais

Branch: `feat/igdb-pr-04-api-casos-uso`; base Back `2c7fac7`. Estado: READY_LOCAL. Escopo G-US-10–12, somente Back. Sem remoto Git configurado; esta descrição prepara a PR, não representa publicação.

## Problema e comportamento resultante

Os endpoints ainda consumiam um catálogo fictício síncrono, e projeções buscavam jogos por item dentro do escopo de publicação. Agora usam o catálogo assíncrono real composto nas PRs anteriores, sempre cache → catálogo local → IGDB para faltantes. O runtime não contém FakeIgdbCatalog nem sementes de reviews/comentários fictícios.

`GET /api/v1/games` retorna `{items,page,pageSize,hasNextPage}`: página inicial 1, tamanho padrão 20, máximo 100. Sem termo usa release-desc de jogos publicados; com termo usa name-asc. Não inventa total. Detalhe mantém GameResponse, IDs canônicos igdb-* e média calculada somente das notas locais. Validação retorna 400; erros do fornecedor usam 502/503/504 seguros, independentes da sessão local.

Reviews, favoritos, opiniões, feeds, dashboard e rankings capturam seus dados locais antes do I/O e resolvem IDs únicos em lotes de até 500. O dashboard compartilha o lote entre favoritos e feed. Jogos conhecidos continuam legíveis no catálogo local após descarte do cache, sem confirmar existência externamente a cada leitura. Comentários usam exclusivamente reviews/autores locais.

CatalogUsage acompanha apenas os metadados utilizados na requisição: stale se qualquer entrada estiver stale; fetchedAt é a data mais antiga. CatalogHeadersFilter emite X-Catalog-Stale e X-Catalog-Fetched-At nas respostas pertinentes, e CORS os expõe. Respostas sem catálogo, como comentários/listas vazias de reviews, não recebem esses headers. OpenAPI vivo e snapshot documentam paginação, metadados e erros.

Escritas preparam jogo/projeção antes de gravar. Publicação, edição, nota e favorito revalidam sessão/cancelamento dentro de IPublicationScope.Execute, sem await ou rede no lock. A resposta usa a projeção preparada e não consulta o fornecedor depois da gravação. Unicidade de review, idempotência e rollback de notificações permanecem protegidos. Edição com falha de projeção conserva o texto original.

## Verificação

- Ciclos RED confirmados e commits de testes antes da implementação para as três US.
- CatalogHttpTests: paginação/defaults, validação, headers/CORS, detalhe, 502/503/504, OpenAPI e ausência de fixtures no host produtivo.
- ProjectionHttpTests: IDs repetidos, lote misto cache/local/externo, leitura offline conhecida, metadados agregados, falha sem perda de review e comentários sem catálogo.
- AsyncWriteTests: espera externa sem lock, concorrência/409, cancelamento, revogação durante a espera para review/nota/favorito, edição sem perda ou consulta posterior.
- Regressões anteriores de notas/média, permissões, feeds/rankings, notificações e rollback agora usam TestApiFactory: fixtures exclusivamente em tests, mantendo resolver/cache/local reais e fornecedor simulado.
- 354 testes Release aprovados (327 Api, 26 Domain, 1 Application), nenhum ignorado; build com analisadores sem avisos/erros. Saída isolada preserva a API aberta pelo usuário. OpenAPI exportado de processo Release temporário em porta livre, encerrado após a exportação.

## Transição e limites

Mudança incompatível: a listagem deixa de ser array. O adaptador Front e os avisos de stale pertencem à G-PR-05; nenhum arquivo Front foi alterado nesta PR. A integração de navegador com esse envelope ainda não foi validada.

Não mapear game-1/game-2/game-3 para jogos reais. Reiniciar a API após atualizar: catálogo/cache e conteúdo local são memórias do processo, sem migração ou persistência. Jogos/opiniões fictícios existem apenas no host de testes. A identidade simulada de Ana permanece para o fluxo local de autenticação.

Nenhum banco, ORM, migration ou nova dependência. Nenhuma consulta live Twitch/IGDB foi executada. Para dados ainda desconhecidos, configurar IGDB__ClientId/IGDB__ClientSecret no processo do servidor; sem configuração a consulta retorna 503 seguro. Swagger permanece em /swagger. Evidências de smoke real ficam para a entrega própria, sem expor credenciais.
