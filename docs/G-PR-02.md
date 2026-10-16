# G-PR-02 — Cliente IGDB e catálogo assíncrono

## Resultado

Preparadas as portas de cache, catálogo local e fornecedor, com coordenação assíncrona em Application. Cache e local atendem antes da IGDB; páginas locais exigem cobertura completa da consulta e batches enviam somente IDs ausentes. Infrastructure constrói queries permitidas e normaliza dados externos em Game com ID estável.

Branch `feat/igdb-pr-02-catalogo-async`, base `5c2ba73` da G-PR-01. Estado READY_LOCAL. Sem remoto configurado, sem PR publicada ou merge. Nenhum arquivo de Front alterado.

## Entregas

- G-US-04: `IAsyncGameCatalog`, `ICatalogCache`, `ILocalGameCatalog`, `IExternalGameCatalog`, paginação e metadados fora de Domain; cancelamento e ordem das portas verificados com spies.
- G-US-05: campos explícitos, busca infix sem case, aspas/backslash escapados, sort permitido, filtro de lançamentos já ocorridos, offset e um item adicional. Detalhe/batch por IDs numéricos únicos, máximo 500.
- G-US-06: DTOs tipados, normalização de ID/capa/gêneros/plataformas/data, campos opcionais e extras, validação de payload e tradução segura 502. Resolução lazy do cliente evita ler credenciais antes de uma consulta externa.

## Verificação

294 testes Release aprovados, sem ignorados: 267 Api, 26 Domain e 1 Application. Build com analisadores: zero erros/avisos. Saída isolada em `bin/IgdbValidation`, preservando a API existente. `git diff 5c2ba73 --check` aprovado.

45 casos novos: AsyncCatalogTests (13), QueryTests (13), GameMappingTests/ExternalCatalogTests (19). RED antes de implementação: `f348cc4` (13 falhas), `a226d67` (12 falhas; dois testes existentes adicionais passaram com o filtro inicial), `658b5fe` (18 falhas), `f9a6cec` (filtro de futuro com termo) e `6a2bd4f` (credenciais lazy).

Domain e Application seguem sem HTTP ou dependências de fornecedor. Nenhum banco, ORM, migration ou pacote de persistência introduzido. Todas as chamadas externas foram simuladas por HttpMessageHandler; nenhuma consulta live foi executada.

## Dependências e transição

A terceira fase agora tem entrega final READY_LOCAL no Front, commit `0981a79`: documentação I-PR-07, runtime HTTP e `docs/evidence/I-PR-07-calls.json` datado 2026-10-14. As evidências existentes registram UI/infraestrutura/permissões separadamente e execução com duas contas e API real. Conferidas por leitura nesta PR; a suíte Front não foi reexecutada aqui. Back permanece baseado em `5c2ba73`; os repositórios não foram misturados.

Esta PR prepara a implementação, sem trocar os endpoints atuais. O contrato legado síncrono IGameCatalog e o runtime fake continuam até a migração de controllers/casos de uso G-US-10–12. As portas assíncronas novas não usam o fake nem All/Find. Cache e catálogo local em memória serão implementados na G-US-19/G-US-08; não foi registrado IAsyncGameCatalog no runtime antes dessas implementações.

O fornecedor real está registrado como IExternalGameCatalog, mas só cria o cliente autenticado quando há I/O externo necessário. O lote vazio não adquire token nem exige credenciais. Não existe endpoint público de OAuth, fields ou APICalypse livre.

Limites restantes: persistência local/cache concreta, TTL/fallback, rate limit e retries gerais na G-PR-03; ativação HTTP/paginação/CORS na G-PR-04; atualização do adaptador Front em PR própria. Smoke live continua pendente.

[Contratos internos e consultas](IGDB-CATALOG.md).
