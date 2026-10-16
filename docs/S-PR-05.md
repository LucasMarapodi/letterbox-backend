# S-PR-05 — Filtros de gênero e plataforma

GET `/api/v1/games` aceita genreId e platformId locais, opcionais e inteiros positivos. Nome AND gênero AND plataforma são aplicados antes da paginação; omissão significa Todos. O envelope existente de jogos e a busca agregada da S-PR-26 foram preservados. Nenhum arquivo Front foi alterado.

## Contrato

```text
GET /api/v1/games/filters
GET /api/v1/games?term=zelda&genreId=1&platformId=2&sort=name-asc&page=1&pageSize=20
GET /api/v1/games?genreId=1&platformId=2&sort=release-desc&page=2&pageSize=20
```

`/games/filters` é público e retorna `{ genres: [{ id, name }], platforms: [{ id, name }] }`. IDs são PKs locais int32, estáveis após upsert/reinício. IDs externos bigint permanecem separados; o consumidor não precisa conhecê-los. Não derivar opções da página exibida nem persistir IDs IGDB como filtro local.

IDs zero/negativos ou opções locais inexistentes retornam 400 com code `invalid_filter`. Formato inválido de query continua 400/validation_failed. A validação de opção inexistente consulta SQL antes de chamar IGDB. Ordenação permanece release-desc/name-asc/name-desc; sem termo, o padrão é release-desc. Tamanhos e envelope de paginação existentes foram mantidos.

## Opções completas e persistência

CatalogFilters compartilha uma sincronização pendente entre solicitantes e mantém por uma hora o cache de opções completas. Lê a fonte SQL antes de pedir metadados. Sem cobertura completa conhecida neste processo, busca gêneros/plataformas pelo adaptador IGDB; ter algumas linhas importadas de jogos não comprova catálogo completo de opções. Após reinício, as linhas mantêm IDs e a cobertura completa é renovada pela integração.

IgdbCatalogFilters pagina id/name por cursor de ID externo, com até 500 registros por chamada, 20 páginas por categoria e orçamento de 30 segundos para a sincronização. Usa os mesmos clientes configuráveis, token, resiliência, limites de transporte e bloqueio de redirects. Não consulta jogos para montar os dropdowns. Resposta inválida ou limite excedido falha sem apresentar um conjunto parcial como completo.

PostgresGameCatalog implementa ICatalogFilterStore: projeta opções do SQL, resolve vínculos local→externo e sincroniza ambas as dimensões em uma transação com o mesmo advisory lock das importações. Upsert preserva PKs e relacionamentos, sem remover dimensões antigas em uso. Falha de plataformas cancela também as inserções de gêneros. Cache de jogos e snapshots são invalidados após sincronização; nenhum store de negócio em memória foi reintroduzido. Somente cobertura/cache são transitórios. Não houve migration; o incidente de isolamento de teste e sua correção estão registrados abaixo.

Cancelar um solicitante cancela sua espera, preservando a aquisição compartilhada para os demais; a operação continua limitada por orçamento e encerramento do host. Cache vencido não transforma opções SQL incompletas em sucesso: falhas mantêm status público de integração/persistência.

## Busca e cache

CatalogSearch e CatalogKey incluem gênero/plataforma, além de termo, ordenação, página e tamanho. Single-flight, cache e snapshots distinguem essas combinações. IGDB recebe apenas IDs externos resolvidos pelo adaptador, com filtros fixos de gêneros e plataformas combinados por AND. Campos/expressões APICalypse não vêm do request; o termo continua escapado. Respostas externas que contradizem os filtros são rejeitadas.

Catálogo local parcial continua parcial. Uma página SQL é reidratada somente com snapshot compatível de cobertura conhecida; não se usa a mera existência de jogos como página global. Reidratação aplica nome/associações/datas e ordenação no SQL, invalidando cobertura se os candidatos não corresponderem. Metadados fetchedAt/isStale e fallback stale elegível continuam no pipeline existente.

## Últimos lançados e desempate

release-desc usa a primeira data de lançamento, exclui futuros/sem data e desempata por ID local crescente. Ordenar somente o lote recebido por ID IGDB não garante essa regra: um jogo conhecido pode ter ID local menor que o de outro com ID externo inferior.

O adaptador identifica os dias UTC na fronteira da página, completa os empates com cursor de ID externo, importa a janela e deixa o PostgreSQL filtrar/ordenar por data/ID local antes de OFFSET/LIMIT. Para páginas avançadas, `/games/count` conta os lançamentos mais novos com os mesmos filtros, convertendo o offset global em offset dentro da janela; não importa todas as páginas anteriores. Teste cobre página 100 e uma identidade local em ordem inversa à externa.

A recuperação fria tem limites: até 2.000 candidatos na janela relevante, 20 consultas lógicas e 30 segundos, sujeitas às tentativas limitadas da resiliência existente. Grupos de empate excepcionalmente grandes ou fonte incoerente não geram página parcial falsa; podem retornar indisponibilidade ou fallback previamente comprovado conforme a política existente. Não há novo limite pelo número da página. Nenhuma requisição externa ocorre dentro de transação SQL. A janela importada não sofre upsert duplicado no pipeline.

As consultas seguem os recursos de [filtros](https://api-docs.igdb.com/#filters), [paginação](https://api-docs.igdb.com/#pagination) e endpoints de [gêneros](https://api-docs.igdb.com/#genre)/[plataformas](https://api-docs.igdb.com/#platform) da documentação oficial consultada. As regras de ordenação por identidade local são responsabilidade do backend.

## Evidência e entrega

RED 0845d81: sete cenários reproduziram parâmetros ignorados, rota de opções ausente e desempate incorreto. RED adicional 6c6f1e6 protege a paginação avançada antes do ajuste por contagem. CatalogFilterTests, CatalogFilterIntegrityTests e CatalogFilterConcurrencyTests cobrem PostgreSQL real isolado, opções independentes de páginas, IDs estáveis, conversão bigint, filtros, SQL antes de paginação, futuro/sem data, cache/snapshots/single-flight distintos, falha parcial de metadados, rollback e cancelamento compartilhado. Transportes externos são controlados somente nos testes.

Na validação, o smoke simulado expôs um desvio de isolamento: o adaptador de lançamentos resolvia o repositório concreto, ignorando o catálogo substituído pelo teste. Isso inseriu três jogos sintéticos (A/B/C, IDs locais 417–419) em gamesdb. A composição agora respeita ILocalGameCatalog/ICatalogReleasePages, mantendo o runtime SQL e as fixtures isoladas. O smoke ganhou uma guarda que lança erro se tentar resolver conexão SQL. As três linhas foram identificadas pelos IDs, conteúdo e timestamps de criação desta execução, conferidas sem vínculos e preservadas em cópia ignorada em TestResults antes da remoção restrita; três exclusões confirmadas, sem reset de banco ou sequência e sem remover conteúdo alheio.

Suíte final: 498 testes aprovados (470 API, 27 Domain, 1 Application), incluindo 13 cenários novos. Apenas smoke IGDB live opt-in ignorado; não houve chamada externa real. Build Release/analisadores sem avisos/erros. OpenAPI exportado com 40 operações funcionais e health; opções com ID integer, parâmetros opcionais documentados, Swagger 200 e erro invalid_filter 400 conferidos em instância temporária encerrada após a verificação. A repetição da suíte não recriou os três registros sintéticos no desenvolvimento.

Branch feat/sexta-05-catalogo-filtros atualizada por fast-forward para a base 372f0e1 antes do desenvolvimento. Configuração privada e exclusão preexistente de .env.example preservadas. Sem remoto configurado, publicação ou merge. A interface de filtros pertence à S-PR-06.
