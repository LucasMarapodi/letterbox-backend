# Catálogo assíncrono e tradução IGDB

## Portas e resolução

Application define ICatalogCache e ILocalGameCatalog como papéis separados de armazenamento; IExternalGameCatalog é somente o fornecedor. Implementações concretas de cache/local ainda pertencem à próxima PR. AsyncGameCatalog implementa IAsyncGameCatalog:

- SearchAsync: cache da consulta exata → snapshot local completo/fresco → fornecedor. Um conjunto parcial de jogos ou página stale não representa cobertura da busca global. Descoberta é salva no catálogo local antes de preencher o cache.
- GetAsync: resolve um ID, retornando null somente para ausência no resultado do fornecedor; erros permanecem erros.
- ResolveAsync: deduplica IDs, consulta cache, consulta local apenas para ausentes e consulta fornecedor apenas para os ainda faltantes. Dados conhecidos e fetchedAt são preservados. Detalhe local desatualizado mantém sua marca, sem forçar rede automaticamente.

CancellationToken atravessa todas as portas e há verificação entre leituras. Não há rede sob lock, nem Result/Wait ou download completo do catálogo. Resiliência/fallback/TTL e snapshots locais concretos serão entregues na G-PR-03.

CatalogMetadata contém fetchedAt e isStale; CatalogGame envolve Game; CatalogPage contém query, items, hasNextPage, metadata e isComplete. Esses tipos são internos de Application, não o DTO HTTP. Domain permanece com o mesmo Game, sem metadados de cache, token, DTO IGDB ou média externa.

## Consultas

CatalogSearch.Create valida termo (trim, máximo 200, sem caracteres de controle), sort, página inicial 1, pageSize de 1 a 100 e offset sem overflow. Defaults: tamanho 20; sem termo, release-desc; com termo, name-asc. Sort permitido: name-asc, name-desc e release-desc.

Campos fixos: id, name, summary, cover.image_id, genres.name, platforms.name e first_release_date. Não solicitar fields *, screenshots, vídeos ou ratings. Queries incluem limit pageSize+1 e offset (page-1)*pageSize para calcular hasNextPage sem inventar total.

Busca usa filtro infix case insensitive com aspas e backslash escapados. Sem termo, filtra first_release_date existente e menor/igual ao instante UTC atual. Ordenação release-desc com termo também exclui futuros. Nenhum endpoint, fields ou expressão fornecida pelo usuário é concatenado como sintaxe livre.

IDs de detalhe/batch são strings canônicas `igdb-{inteiro positivo}`, representadas externamente por números. Rejeitar game-1, zero, negativos, zeros à esquerda, overflow e expressões. Batch máximo de 500 IDs únicos; vazio não faz requisição. IGDB é chamada somente na URL fixa `/v4/games` pelo cliente autenticado da G-PR-01.

## Normalização

- id → ID público igdb-*; name obrigatório → title com trim.
- summary ausente/null → string vazia.
- cover.image_id validado (ASCII alfanumérico/underscore, máximo 128) → `https://images.igdb.com/igdb/image/upload/t_cover_big/{image_id}.jpg`. URL/host extras do fornecedor não são usados; ausência válida → null.
- genres.name/platforms.name → listas de nomes únicos, sem entradas vazias; ausentes → listas vazias.
- first_release_date Unix em segundos → DateOnly UTC; ausente → null; fora da faixa → erro seguro.
- rating/aggregated_rating/enums/campos extras não são importados; a média continua pertencendo às notas locais.

JSON não-array, null, tipos incompatíveis, linhas null, IDs repetidos, IDs não solicitados, nome/ID inválido e capa/data inválida geram CatalogInvalidResponseException sem body/inner exception do fornecedor. O pipeline traduz para 502/integration_invalid_response. Status externo não-200 gera indisponibilidade; recuperação de 401 segue a G-PR-01, sem modificar sessão local.

## Estado do runtime

Na G-PR-04 os endpoints e casos de uso passaram a usar IAsyncGameCatalog, com cache e catálogo local concretos. O fornecedor permanece lazy: somente faltantes precisam de OAuth/IGDB. A listagem usa items/page/pageSize/hasNextPage; detalhe e projeções usam igdb-*. Não há fake ou sementes de opiniões no runtime. Consulte [G-PR-04](G-PR-04.md) para atomicidade, metadados, validação e mudança incompatível para o Front, ainda não alterado.

Testes usam transporte fake, spies e datas sintéticas. Não comprovam uma consulta live à IGDB. Fontes primárias consultadas: [filtros](https://api-docs.igdb.com/#filters), [paginação](https://api-docs.igdb.com/#pagination) e [imagens](https://api-docs.igdb.com/#images).
