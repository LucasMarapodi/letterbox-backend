# P-PR-03 — Reviews e avaliações

Reviews e notas passam a persistir no PostgreSQL. Reiniciar a API preserva publicações, edições e avaliações; as médias são calculadas pelo SQL, com null para ausência de notas e zero como valor válido.

## Funcionalidade e contrato

- Publicar review retorna ID local inteiro gerado pelo banco. A restrição única usuário/jogo resolve concorrência com uma criação e conflitos 409 nas demais tentativas.
- Somente o autor edita; ID, autoria, jogo e data de publicação são preservados. Texto vazio ou acima de 10.000 caracteres é rejeitado.
- Avaliar usa upsert atômico por usuário/jogo, preservando ID e criado_em. A nota aceita decimais finitos de 0 a 5; atualizado_em não retrocede em requisições concorrentes.
- Listagens por jogo, autor, seguidores e favoritos aplicam filtros no SQL. Projeções de jogos/autores e médias são carregadas em lotes, sem consulta de média por item.
- Reviews, avaliações e referências reviewId agora são números int32 nos contratos. IDs próprios de comentários, favoritos e notificações continuam transitórios; targetId polimórfico dos favoritos permanece texto decimal até sua normalização.
- Falha de persistência retorna 503/storage_unavailable, sem fallback para opiniões em memória.

## Código e schema

OpinionConfigurations mapeia reviews e avaliacoes: PKs integer identity, FKs integer para usuarios/jogos com RESTRICT, unicidade por usuário/jogo, datas UTC e índices de consulta. Reviews têm CHECK de texto não vazio; notas usam double precision com CHECK de faixa, incluindo rejeição de NaN/infinito. RatingValue mantém as mesmas invariantes no domínio, sem referência a EF/Npgsql.

PostgresReviews implementa publicação transacional, leitura AsNoTracking, filtros SQL e edição restrita ao autor. PostgresRatings usa INSERT ON CONFLICT parametrizado e AVG agrupado no banco. IReviews/IRatings são portas assíncronas com CancellationToken; stores de opiniões em memória existem somente nos testes. DbContext e repositórios são scoped.

Publicação prepara notificações após obter o ID gerado e antes de confirmar a transação. Uma falha nessa preparação cancela a review SQL e os eventos pendentes; os eventos em memória só ficam visíveis após commit. O contexto permite que a futura persistência de notificações participe da mesma transação. Nenhum lock síncrono de publicação permanece adquirido durante I/O SQL.

## Limites da transição

Comentários, favoritos, seguimentos e notificações ainda são dados em memória. Notificações não têm garantia durável neste estágio: uma interrupção entre commit SQL e publicação em memória pode perder o evento. A atomicidade durável entre review e notificação depende da PR de notificações. Dashboard/feed combinam snapshots locais com leituras SQL, sem snapshot transacional global entre esses stores.

O Front não foi alterado. Seu consumidor precisa aceitar também IDs numéricos de reviews/avaliações e referências reviewId antes de liberar o contrato. Nenhum conteúdo antigo em memória foi importado ou declarado persistente.

## Validação e operação

Commit RED 218052f antes da implementação. PostgreSQL 17 real exclusivo de testes: materialização, unicidade concorrente, upsert concorrente preservando ID, edição/autoria, texto inválido, reinício, médias agrupadas, null versus zero, preservação de criado_em, rollback de preparação de notificações e constraints SQL de texto/faixa/FKs.

397 testes aprovados; smoke IGDB opt-in ignorado nesta execução. Build Release com analisadores: zero avisos e erros. Swagger, health e OpenAPI conferidos localmente; documento exportado atualizado.

Migration Opinions aplicada em tcc_pr01_tests e gamesdb. Upgrade local preservou as 5 contas e os 42 jogos existentes, adicionando somente duas tabelas e a entrada no histórico. Nenhuma migration ou limpeza no startup; configuração privada permanece no .env ignorado.

Branch feat/postgres-pr-03-opinioes, P-US-18–21. Sem remoto configurado: entrega local para revisão, sem publicação ou merge.
