# Banners e destaque — V-PR-07 / V-US-09

Entrega de backend, sem upload, download de imagem no servidor, storage/SDK, alteração do Front ou código mobile. A implementação foi adiantada em branch isolada após a confirmação do usuário em 19/11/2026; a revisão final da V-PR-06 ainda está pendente. Esta PR não declara concluído o gate visual US01–08 nem integra a V-PR-08.

## Banner do perfil

`UserProfile.BannerUrl` é opcional, público como imagem de apresentação, e usa a mesma validação de imagens do avatar: HTTPS absoluto, até 2048 caracteres. PATCH `/api/v1/users/me/profile`: omitir preserva; null/vazio/espaços remove; URL válida altera. A conta vem exclusivamente do Bearer; não há campo de identidade para escolher outro dono.

```json
{"bannerUrl":"https://example.com/banner.jpg"}
```

PUT `/api/v1/users/me/profile-completion` mantém a mesma semântica de omissão/remoção do banner. Respostas públicas de usuário (incluindo login/me) e privadas de edição/conclusão recebem `bannerUrl` nullable. País/estado e favoritos continuam privados. Banner não altera avatar, senha, sessão, favoritos ou cálculo de `isComplete`; salvar perfil incompleto continua permitido. Perfil e preferências usam a transação/lock existentes.

## Banner do jogo

`Game.BannerUrl` é persistido e adicionado a `GameResponse`. A query IGDB solicita artworks e screenshots expandidos com image_id/width/height. Seleciona artwork horizontal válido (largura > altura); sem artwork apropriado, usa screenshot horizontal. Dentro da fonte, prefere a maior área, desempate por image_id. Imagem sem dimensões, vertical ou com identificador inválido não é candidata. Capa nunca vira banner.

A URL HTTPS é construída sob a configuração IGDB__ImagesBaseUrl usando `t_1080p/{image_id}.jpg`, formato Fit documentado na [IGDB](https://api-docs.igdb.com/#images). Nada é baixado pelo backend. Queries em lote e cache existentes são preservados.

`BannerSpecified` é somente um sinal interno de importação, não coluna nem campo público. Imagem válida recebida substitui a anterior; se artworks/screenshots forem omitidos/parciais sem candidato, preserva o banner conhecido. Ambos os arrays explicitamente recebidos sem imagem apropriada removem o banner. Upsert mantém a proteção contra respostas antigas. Cache copia o record com o banner; leituras e projeções usam o catálogo local.

## Destaque

`GET /api/v1/home/featured` é público/no-store e retorna:

```json
{"game":{"id":1,"title":"Exemplo sintético","summary":"Descrição","coverUrl":null,"bannerUrl":"https://example.com/game.jpg"},"selectionReason":"trending"}
```

O exemplo descreve o formato, não é fixture de produção. A seleção real usa avaliações/jogos SQL com banner HTTPS, filtrando imagens antes do recorte. Prioriza última avaliação DESC/ID ASC de Em alta; fallback segue média DESC, quantidade DESC, última avaliação DESC, ID ASC de Mais bem avaliados. Sem candidato: `{"game":null,"selectionReason":"no_candidate"}`; motivo do fallback: `top_rated`. Não escolhe jogo sem nota/capa vertical nem consulta IGDB para renderizar o hero. Falha SQL permanece 503/storage_unavailable.

## Migration e operação

`20261120021553_ProfileAndGameBanners` adiciona apenas `banner_url varchar(2048) NULL` em `perfis_usuarios` e `jogos`, sem default/backfill/tabela nova. Preserva IDs, campos, relações e FKs RESTRICT. Down remove somente as duas colunas e perde seus valores. Não há migration no startup.

Aplicada somente no PostgreSQL exclusivo de testes, validado por nome/host/marcador. O banco de desenvolvimento ainda exige upgrade explícito antes de executar esta versão, conforme [POSTGRES.md](POSTGRES.md). Não houve alteração de credenciais ou dados reais. A validação visual/integrada do frontend permanece nas V-PR-06/08.

Validação essencial em 19/11/2026: somente três testes novos, registrados em RED em e07dd81; 59 verificações direcionadas aprovadas (51 API, 8 Domain), incluindo integridade/upgrade das 14 migrations e regressões existentes de perfil, contratos e mapper. Build Release/analisadores sem avisos ou erros. Suíte completa e testes mobile não foram executados. Health, Swagger e OpenAPI da API HTTPS temporária retornaram 200; contrato exportado e instância encerrada.
