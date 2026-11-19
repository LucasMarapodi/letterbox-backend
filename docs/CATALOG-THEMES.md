# Temas do catálogo — V-PR-02 / V-US-02

O catálogo passa a importar `themes.id`/`themes.name` na mesma consulta IGDB que obtém gêneros, plataformas e dados do jogo. A expansão usa a [documentação oficial IGDB](https://api-docs.igdb.com/#expander). Não existe chamada externa por card, serviço de IA ou importação integral de temas. Não foram implementados rankings/recomendações, franquias ou banners nesta entrega.

## Modelo e persistência

- `Theme` / `temas`: `id` integer identity local, `igdb_id` bigint positivo/único, `nome` varchar(200) obrigatório. Nomes são aparados, sem substituir os nomes reais por categorias inventadas. Payloads inválidos ou nomes conflitantes para o mesmo ID externo são rejeitados como resposta inválida do fornecedor.
- `GameTheme` / `jogos_temas`: `jogo_id` e `tema_id` integer, PK composta, índice inverso por tema e duas FKs RESTRICT. Um jogo possui vários temas e um tema pertence a vários jogos.
- `Game.ThemesFetchedAt` / `jogos.temas_obtidos_em`: timestamp UTC nullable, sem default/backfill. Distingue jogo antigo/metadado não recebido de um conjunto explicitamente recebido, inclusive vazio. Não é preferência de usuário nem estado público do contrato.

Migration aditiva `20261119205619_CatalogThemes`: cria as duas tabelas e acrescenta a coluna nullable; não modifica gêneros, plataformas, jogos existentes ou identidades. Down remove somente os vínculos/tabela/coluna novos e perde seus metadados. Não é necessário executar Down para atualizar.

Upsert reutiliza a transação SQL e o lock de importação existentes. Dimensão usa conflito por igdb_id; vínculos são reconciliados somente quando o campo de temas foi recebido. Repetição não duplica tema/vínculo. Resposta com temas ausentes/null preserva temas conhecidos e sua data; array explicitamente vazio remove os vínculos daquele jogo. Uma resposta mais antiga não sobrescreve o jogo nem suas associações. Leituras resolvem temas de todos os IDs do lote em uma consulta join adicional, sem N+1.

## Contrato e atualização

`GameResponse` recebe o campo aditivo `themes: string[]`, mantendo os campos anteriores. Aparece em listagem, detalhes, descoberta por IGDB e demais projeções que reutilizam o DTO. Não expõe IDs externos de temas, marcador de obtenção ou dados de preferências. Sem metadados, retorna `[]`; o gênero conhecido continua disponível para a futura recomendação.

```json
{"id":1,"igdbId":3000000001,"title":"Exemplo sintético","themes":["Fantasy"]}
```

O exemplo é parcial e sintético, não uma resposta IGDB live. GET /api/v1/games e GET /api/v1/games/{id} continuam públicos e paginados/conforme contrato existente. Não há novo endpoint de temas ou mudança nos filtros de gênero/plataforma.

Cache → SQL → IGDB permanece igual. Uma leitura local conhecida não força rede apenas porque faltam temas. Busca sem cobertura e `RefreshAsync` existente enriquecem os metadados quando consultam o fornecedor; não há migração/backfill de toda a IGDB nem sincronização presumida de registros antigos. Falha externa elegível no refresh mantém o jogo conhecido. Cache copia também as coleções de temas como readonly. SQL confirma antes de popular cache, preservando cancelamento e rollback da importação.

## Operação

Esta PR gera a migration e a aplica somente no PostgreSQL exclusivo de testes, validado pelo nome/host/marcador existentes. O banco de desenvolvimento ou hospedado precisa receber o upgrade explicitamente antes de executar o novo código; não há migration automática no startup. Usar a configuração segura e o comando descritos em [POSTGRES.md](POSTGRES.md), nunca conexão em argumento de CLI ou chave versionada. Execução web permanece [HTTPS](HTTPS.md).

O frontend permanece inalterado. V-PR-03 poderá usar Theme/GameTheme para relacionar favoritos a candidatos, sem criar outra fonte de preferências.

## Validação — 19/11/2026

686 testes aprovados (84 Domain, 14 Application, 588 API), um smoke IGDB externo optativo ignorado; build Release/analisadores sem avisos ou erros. Auditoria das 13 migrations e 21 tabelas aprovada, incluindo upgrade com jogos/gêneros antigos preservados, FKs RESTRICT, upsert sem duplicação, metadata ausente/vazio, refresh/fallback e contrato público após reiniciar a API. OpenAPI adicional validado com themes obrigatório também nas projeções aninhadas. Health/Swagger/OpenAPI da instância HTTPS temporária responderam 200; documento reexportado. Instância encerrada; sem IGDB live ou alteração no banco de desenvolvimento.
