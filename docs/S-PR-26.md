# S-PR-26 — API agregadora de pessoas e jogos

GET `/api/v1/search` é público e consulta Pessoas e Jogos em uma única resposta. Os grupos têm estado independente: falha em uma fonte preserva a outra. Não foram alterados arquivos do Front; menu e página de resultados pertencem às S-PR-27/28.

## Contrato para os consumidores

```text
GET /api/v1/search?q=mario
GET /api/v1/search?q=mario&mode=results&scope=all&page=1
GET /api/v1/search?q=mario&mode=results&scope=people&page=2
GET /api/v1/search?q=mario&mode=results&scope=games&page=2
```

q recebe trim e deve ter 3–200 caracteres, sem controles. mode aceita suggestions (padrão, cinco itens por grupo, somente page=1) ou results (20 por grupo). scope aceita all (padrão), people ou games. page deve ser inteiro positivo, com proteção de overflow. pageSize é rejeitado: os tamanhos são fixos. Query inválida retorna 400/validation_failed antes de consultar fontes.

O envelope contém query normalizada, people, games e error. Cada grupo tem status enum serializado como ready, unavailable ou notRequested; items, page, pageSize, hasNextPage, error e metadata. Campos não aplicáveis são null.

| Estado | Conteúdo e paginação | Erro |
| --- | --- | --- |
| ready | Items públicos; page/pageSize/hasNextPage válidos. Vazio é sucesso real. | null |
| unavailable | Items vazio; paginação null. Não interpretar como consulta vazia bem-sucedida. | code/message públicos |
| notRequested | Items vazio; paginação null. O consumidor pode preservar o resultado anterior desse grupo. | null |

Pessoas retorna somente `{ id, username, displayName, avatarUrl }`. Jogos retorna somente `{ id, title, coverUrl, summary, releaseDate }`. IDs são locais int32, sem substituir por identificadores externos. metadata de Jogos ready contém fetchedAt/isStale provenientes do catálogo; não existe metadata fictícia para grupo indisponível. Metadados estão no grupo para não atribuir o estado de Jogos a Pessoas por headers globais.

200 significa que pelo menos um grupo solicitado está ready. Quando todos os solicitados falham, 503 mantém o mesmo envelope e acrescenta error `{ code: search_unavailable, message: Busca temporariamente indisponível. }`. Cada grupo identifica sua falha com storage_unavailable, integration_unavailable, integration_timeout ou integration_invalid_response. Categoria fora do scope continua notRequested. Não há mensagem de exceção, conexão, e-mail, token ou segredo no contrato.

Exemplo de falha parcial:

```json
{
  "query": "mario",
  "people": {
    "status": "ready", "items": [], "page": 1, "pageSize": 5,
    "hasNextPage": false, "error": null, "metadata": null
  },
  "games": {
    "status": "unavailable", "items": [], "page": null, "pageSize": null,
    "hasNextPage": null,
    "error": { "code": "integration_unavailable", "message": "Integração externa indisponível." },
    "metadata": null
  },
  "error": null
}
```

Retry de Jogos envia o mesmo q/mode/page com scope=games; não consulta Pessoas. A mesma regra vale no sentido inverso. Para paginação independente, consultar só a categoria cuja página mudou.

## Código, SQL e limites

SearchRequest valida entrada. AggregatedSearch coordena IPeopleSearch e IAsyncGameCatalog em sequência: nunca inicia duas operações sobre o mesmo DbContext scoped. Não faz HTTP para controllers internos. CancellationToken atravessa as fontes e é conferido entre elas; cancelamento do solicitante não vira falha parcial nem dispara a próxima fonte. Prazos SQL/Npgsql e orçamento de integração existentes continuam ativos, sem retry adicional no agregador.

PostgresPeopleSearch usa ILIKE parametrizado em displayName OU username. Um @ inicial é removido somente da comparação de username; %, _ e barra invertida são literais escapados. Ordem: username exato, prefixo de username ou displayName, demais; desempate por username e ID. Faz projeção SQL dos quatro campos públicos, OFFSET/LIMIT de pageSize+1 para hasNextPage, sem contar/carregar todos os perfis, sem N+1 e sem pesquisar e-mail/bio.

O plano EXPLAIN de consulta equivalente foi conferido no PostgreSQL exclusivo: LIMIT, ordenação e hash join com scans nas tabelas pequenas. Índices PK/FK e username único existentes foram preservados. Correspondência parcial com `%termo%` não se beneficia genericamente de B-tree; esta entrega não promete busca indexada por substring nem adiciona extensão/migration sem medição que justifique. Se o volume crescer, medir planos/latência e avaliar estratégia de índice em entrega própria.

Jogos reutiliza o pipeline real cache → catálogo SQL com cobertura → IGDB; catálogo local parcial não vira cobertura global. Importação externa persiste antes de cache/resposta, preservando IDs locais. Os testes usam fornecedor controlado; nenhuma fixture foi adicionada ao runtime. O agregador não consulta avaliações ou perfis extras para preencher a projeção mínima de jogos.

## Evidência e entrega

RED `772408f`: 13 cenários falharam por rota ausente (404), antes do código. AggregatedSearchTests cobre SQL real, nomes duplicados, ordenação/paginação, @username, padrões literais, privacidade da projeção, integração/cache/importação, scope, falhas parciais nos dois sentidos e falha total. Interceptor confirma SELECT sem e-mail/hash/bio e que retry de Jogos não executa a query de Pessoas. AggregatedSearchBehaviorTests cobre cancelamento, sequenciamento, isolamento de scope e preservação de metadata stale. OpenAPI/matriz de permissões incluem a rota pública e o envelope 503 específico.

Suíte completa: 485 testes aprovados (457 API, 27 Domain, 1 Application), incluindo 22 cenários novos. Apenas smoke IGDB live opt-in ignorado; nenhum acesso externo real. Build Release/analisadores sem avisos ou erros. OpenAPI exportado e conferido em instância temporária, encerrada após verificar estados tipados, IDs integer, query inválida 400 e indisponibilidade total 503 com envelope estruturado.

Base exclusiva de testes; nenhum dado de desenvolvimento ou schema alterado. Branch feat/sexta-26-busca-agregada-api, sem remoto configurado, publicação ou merge. A parte API da S-US-24 está pronta localmente; a US completa depende das S-PR-27/28.
