# G-PR-06 — Segurança e smoke do backend

Branch `feat/igdb-pr-06-validacao-back`, base Back `fec0fa8`. G-US-15 concluída; harness da G-US-16 implementado, smoke live BLOCKED_ENV. Sem remoto Git, sem publicação ou merge.

## Resultado funcional e de código

O backend recebe configuração local pelo .env ignorado pelo Git em Development, com prioridade para as variáveis do processo. LocalIgdbEnvironment lê somente as três chaves IGDB, de forma lazy, sem modificar o ambiente global ou incluir conteúdo/caminho em erros. LaunchSettings não contém credenciais. A identidade local permanece independente do token Twitch.

SecurityRegressionTests exercitam endpoints reais com transporte simulado em Trace: OAuth 302/400; IGDB 302/401/429/500/JSON inválido. Sentinelas no token, body e Location do fornecedor não aparecem em logs, respostas públicas, headers ou OpenAPI; auth/me continua 200 após erro externo. A arquitetura confirma ausência de pacotes de persistência, referências a testes e catálogo fake em produção. Suites anteriores preservam permissões, contratos, médias locais, concorrência e rollback.

InMemoryCatalogCache.EvictGame remove somente a entrada de detalhe indicada, sob o mesmo lock. Não altera snapshots, outros jogos ou catálogo local; não há endpoint público de limpeza. CacheEvictionRegressionTests confirma remoção, reidratação via catálogo local e projeção offline com zero chamadas ao fornecedor.

LiveSmokeScenario e LiveIgdbSmokeTests ficam exclusivamente no assembly de testes. O smoke usa a API real via TestServer e o transporte real de produção, sem depender da API aberta pelo usuário. scripts/igdb-smoke.ps1 exige -Live e executa somente Category=IgdbLive. Sem opt-in, o teste live é ignorado; sem configuração utilizável, o relatório é BLOCKED e nenhuma chamada é feita.

O cenário verifica recentes, repetição do cache, detalhe, evicção seletiva/local hit, próxima página quando existente e busca por termo. Mantém fetchedAt, IDs e média local. Contadores sem headers/bodies limitam a 2 chamadas OAuth e 6 IGDB; sem retries transitórios, 1 req/s e uma chamada simultânea, orçamento global de 90s e orçamento de catálogo de 30s. Reutilização do token é verificada sem aguardar expiração real; renovação permanece coberta por relógio sintético. Logs são desativados no harness live. Apenas status, contagens, IDs e metadados entram na evidência JSON.

## Evidências observadas

- 374 testes determinísticos Release aprovados: 347 Api, 26 Domain, 1 Application; nenhum ignorado com Category!=IgdbLive.
- Build Release com analisadores: zero erros e avisos. OutputPath bin/IgdbPr06/ preserva a API local que usa os binários anteriores.
- LocalEnvironmentTests: 5 casos sintéticos; SecurityRegressionTests: 8 casos; CacheEvictionRegressionTests: 1 caso; SmokeScenarioTests: 6 casos simulados, incluindo bloqueio antes de iniciar o host.
- Ciclos RED/commits antes de implementação do leitor local, evicção seletiva e harness. A matriz de segurança verificou comportamentos existentes sem criar defeitos artificiais.
- Execução opt-in em 2026-10-14: BLOCKED_ENV, as duas chaves do .env ainda são placeholders 123; 0 chamadas OAuth, 0 IGDB. [Relatório sanitizado](evidence/G-PR-06-live.json). Isso não valida a integração live.
- Nenhum arquivo Front alterado por esta entrega. A G-PR-05 já foi entregue pelo trabalho paralelo do Front, conforme sua descrição versionada; mudanças locais paralelas foram preservadas.

## Executar

Na raiz Back, preencha as credenciais no .env local (não versionado) ou nas variáveis do processo. O runner recusa valores 123. Não passar credenciais na linha de comando.

```powershell
dotnet test --no-restore -c Release -p:OutputPath=bin/IgdbPr06/ --filter 'Category!=IgdbLive'
dotnet build --no-restore -c Release -p:RunAnalyzers=true -p:OutputPath=bin/IgdbPr06/
./scripts/igdb-smoke.ps1 -Live
```

O script restaura o opt-in anterior ao terminar. Saída não aprovada gera erro seguro e relatório FAILED/BLOCKED; não é transformada em sucesso da suíte. Arquivo docs/evidence/G-PR-06-live.json contém a última tentativa, nunca credenciais ou payloads. Smoke real e confirmação dos metadados/cache live seguem pendentes até a configuração válida.

## Condições de uso consultadas

A documentação oficial permite cache local e descreve uso não comercial sob o acordo de desenvolvedor Twitch. Para produtos monetizados, descreve parceria e atribuição visível em posição estática. Esta revisão documental não executa parceria nem implementa atribuição no Front. Os limites atuais publicados são 4 req/s e 8 simultâneas; o smoke fica abaixo deles. Fontes consultadas em 2026-10-14: [IGDB — FAQ de negócio](https://api-docs.igdb.com/#business-related-faq), [IGDB — limites](https://api-docs.igdb.com/#rate-limits) e [acordo Twitch](https://www.twitch.tv/p/en/legal/developer-agreement/).
