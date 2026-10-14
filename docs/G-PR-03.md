# G-PR-03 — Cache, catálogo local e disponibilidade

## Resultado

Implementadas G-US-07, G-US-19, G-US-08 e G-US-09. O fornecedor possui limite por processo, fila finita e orçamento de retry/tempo. Cache temporário e catálogo local são estruturas independentes em memória. Consultas seguem cache → local → IGDB; jogos conhecidos continuam legíveis sem rede, e buscas globais usam somente snapshots completos da página exata.

Branch `feat/igdb-pr-03-cache-resiliencia`, base `3914c79` da G-PR-02. READY_LOCAL, sem remoto configurado/publicação/merge. Front não alterado. Dependência da terceira fase conferida na entrega Front `0981a79` e seus artefatos já versionados; testes Front não reexecutados nesta PR.

## Funcionalidade e código

- IgdbResiliencePolicy/IgdbCallBudget: rolling window de 3 starts/s, máximo 4 simultâneas e 32 em fila. Máximo 2 retries para 429/5xx, backoff com jitter, Retry-After delta/data, cooldown compartilhado para 429. Budget total de 30s inclui aquisição/espera do token e as tentativas IGDB, sem reiniciar após recuperação de 401. Resposta tardia é rejeitada; cancelamento libera a capacidade. OAuth compartilhado conserva seu próprio timeout de transporte e pode continuar para outros solicitantes.
- InMemoryLocalGameCatalog: upsert por ID, preservação de dados mais recentes, fetchedAt original e todos os jogos conhecidos durante o processo. Snapshots guardam IDs ordenados, query/hasNextPage/metadados; limite de 128 com LRU. Evicção de snapshots/cache não remove jogos, incluindo os referenciados por opiniões e favoritos. Sem arquivo de persistência ou banco.
- InMemoryCatalogCache: LRU de até 512 jogos e 128 páginas; chave inclui termo normalizado, sort, página e tamanho. Cache de detalhe expira após 30min de residência; freshness externa continua baseada no fetchedAt original. Páginas frescas por 5min, retidas para fallback por até 60min adicionais. Repopulação não fabrica uma nova data nem remove marca stale.
- SingleFlightGameCatalog: chamadas da mesma página/mesmo conjunto de IDs/mesmo refresh compartilham operação; cada solicitante pode cancelar sua espera. Book de até 128 chaves pendentes. Sob sobrecarga, IKnownCatalogReader mantém acesso a dados já disponíveis em cache/local, sem aumentar chamadas ao fornecedor.
- AsyncGameCatalog: fallback somente para indisponibilidade transitória/timeout; primeiro cache elegível, depois local elegível. Não mascarar payload inválido, rejeição 400/404 do fornecedor ou autenticação externa persistente. RefreshAsync consulta as camadas antes da tentativa explícita; falha não apaga dados, ausência confirmada não remove opiniões/referências locais. Cache fresco não recebe marca stale artificial.
- CatalogMemoryRegistration: stores e coordenador singleton por host. ScopedExternalGameCatalog mantém escopo próprio durante o I/O compartilhado; cancelar um request de usuário não descarta o cliente usado por outros waiters. Nenhuma credencial é lida em um local hit.
- Pipeline: 502/integration_invalid_response, 503/integration_unavailable, 504/integration_timeout, sem payload/inner exception secretos. Sessão local de usuário permanece independente do token Twitch.

## Configuração sem credenciais

`IGDB:Resilience` aceita propriedades de IgdbResilienceOptions; `IGDB:CatalogMemory`, as de CatalogMemoryOptions. Defaults acima. Valores são validados e não podem exceder 4 req/s, 8 simultâneas, 2 retries ou fila 128. TotalTimeout até 1min; fallback de página até 60min. Credenciais continuam exclusivamente nas variáveis do processo, não nessas seções.

Uma instância por credencial: os limites não são distribuídos entre processos. Games conhecidos permanecem durante o host e podem aumentar conforme descoberta; somente cache, snapshots e consultas pendentes têm limites de descarte. Reiniciar perde todos esses dados, tokens e estado local.

## Evidência

332 testes Release aprovados (305 Api, 26 Domain, 1 Application), nenhum ignorado. Build RunAnalyzers=true: zero erros/avisos; `git diff 3914c79 --check` aprovado. OutputPath=bin/IgdbValidation preservou a API local em execução.

38 casos novos: ResilienceTests (15), LocalCatalogTests (6), CatalogCacheTests (5), CatalogCompositionTests (1), CatalogAvailabilityTests (11). HTTP fake, spies, relógio/scheduler controlado; um teste de timeout usa timer real com intervalo curto e sem rede.

Commits RED antecedem implementações: cd92f82/bf3634e (resiliência/budget), afc4b8c (local), 1680516/8c0d90d (cache/composição), c01e7f7/f82c1a5 (stale/refresh/erros), 2c11044 (resposta tardia), 595d001 (leitura local durante sobrecarga). Configuração, cancelamento, TTL e dados sintéticos não expõem segredos.

## Limites da entrega

IAsyncGameCatalog está composto no host, mas os endpoints públicos ainda utilizam o contrato legado/fake. A troca de controllers/casos de uso, paginação e headers CORS ocorre na G-PR-04. Não foi introduzido SQL, ORM, migrations, frontend ou chamada live Twitch/IGDB. O catálogo real ainda não foi observado pelo navegador.

Referência primária de limites: [IGDB rate limits](https://api-docs.igdb.com/#rate-limits), consultada em 2026-10-14.
