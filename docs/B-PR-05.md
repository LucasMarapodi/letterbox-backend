# Dashboard, feeds e rankings da Comunidade

A API passa a oferecer Início pessoal com favoritos/feed e uma Comunidade pública independente. Rankings contam comentários recebidos na janela selecionada, inclusive em reviews antigas, e não confundem data de publicação, média de notas ou popularidade externa com discussão.

Branch `feat/back-pr-05-dashboard-feeds`, baseada em `d480d5c` da PR-4. Implementação exclusiva no repositório Back; nenhum merge realizado.

## Entregas

- B-US-19: feed do token com relações atuais, publicação decrescente/ID crescente, vazio válido e alterações refletidas após seguir/deixar de seguir ou publicar.
- B-US-20: DashboardResponse nomeado composto das mesmas consultas de favoritos/feed e projeção de média local, com estrutura vazia estável e isolamento entre contas.
- B-US-21: reviews recentes públicas incluindo autores não seguidos; edição/comentário em review antiga não altera sua posição por publicação.
- B-US-22: RankingPeriod 24h/7d/30d, padrão 7d, janelas inclusivas em horas UTC, exclusão de futuros, contagem positiva, lastCommentAt elegível e desempates determinísticos.
- B-US-23: soma dos comentários de reviews distintas por jogo usando a mesma seleção temporal/ordenação. Atividade não depende de nota local ou IGDB.

## Arquitetura e validação

RankingPeriod é objeto de valor do domínio. Application compõe consultas/projeções, captura relógio uma vez para selecionar comentários e usa o escopo compartilhado para snapshots de publicação/relações. Não há novas entidades armazenadas, caches ou chamadas externas. ReviewProjection e a projeção de favoritos são reutilizadas para evitar duplicação de regras.

Todas as cinco US tiveram RED executado e commit de testes antes da implementação. Testes/build pertinentes foram executados antes dos commits GREEN.

Validação final em 12/10/2026, SDK 10.0.401:

- `dotnet test --no-restore`: 158 testes aprovados, sem ignorados (131 Api, 26 Domain, 1 Application).
- `dotnet build --no-restore -c Release`: sucesso, zero avisos e erros, com nullable e análises habilitados.
- `git diff --check`: sem erros. Cobertura inclui sessões, alterações refletidas, acesso público/privado, listas vazias, empates por ID/data, três períodos, limites exatos, comentários futuros, múltiplas reviews do mesmo jogo e comentário HTTP em review antiga com isolamento entre hosts.

## Escopo e publicação

Front preservado. Sem paginação/limites silenciosos, entidades de dashboard, banco ou integração externa. Contratos e exemplos: [FEEDS.md](FEEDS.md). OpenAPI e validação final de compatibilidade são a PR-6; a migração do frontend para HTTP permanece uma entrega específica.

Sem remoto configurado: nenhuma PR remota foi criada. Falta definir remoto e branch base de destino para publicação; a base local utilizada é a entrega anterior.
