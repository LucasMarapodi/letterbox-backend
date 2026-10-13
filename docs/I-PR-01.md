# I-PR-01 — CORS e contratos para integração

## Resultado

O backend passou a responder preflights e liberar leitura de sucessos e erros para origens explicitamente configuradas. A política inclui JSON/Bearer, métodos usados pela aplicação e exposição de Location, sem credenciais de cookie. Contratos existentes foram revisados contra o consumidor TypeScript e receberam verificações complementares do JSON, sem mudança nos DTOs.

Repositório: Back. Branch: `feat/integracao-pr-01-cors`. Base local: `0b52e29` (`feat/back-pr-06-validacao`), entrega final da segunda fase. Não há remoto configurado; esta é a descrição preparada para uma PR local, sem publicação ou merge.

## Escopo

- I-US-01: [CORS configurável](CORS.md), middleware e testes de origens, preflights e erros.
- I-US-02: [inventário, formatos e evidências de contrato](HTTP-INTEGRATION.md), cobertura HTTP complementar e leitura dos contratos de Front.
- Nenhum arquivo de Front alterado. A API permanece em memória com token simulado e catálogo fake.

## Validação

- RED CORS: 14 falhas esperadas por headers ausentes e preflight 405, 4 casos negativos aprovados; testes commitados em `edc1661` antes da implementação.
- 31 casos novos: 25 CORS e 6 de contratos do consumidor. Testes de contrato complementam o comportamento já existente; não houve correção funcional de DTO que exigisse RED adicional.
- Suíte Release: 198 testes aprovados, sem ignorados.
- Build Release com `RunAnalyzers=true`: zero erros e avisos.
- `git diff 0b52e29 --check`: aprovado.

## Integração ainda pendente

As evidências são testes HTTP de backend, não consumo de interface. As próximas PRs implementam os adaptadores de Front, sessão e fluxos; a validação final de CORS no navegador continua pendente. O relatório da terceira fase conserva o consumo funcional como PLANEJADO.
