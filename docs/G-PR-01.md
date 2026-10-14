# G-PR-01 — Credenciais e token Twitch

## Resultado

Implementada a infraestrutura de autenticação externa: credenciais exclusivamente do processo backend, clientes HTTPS sem redirects/logs HTTP, aquisição OAuth por formulário, token em memória, aquisição compartilhada e renovação antecipada. Um 401 IGDB admite uma recuperação e uma repetição; falha persistente vira erro seguro 503 e preserva a sessão local.

Branch `feat/igdb-pr-01-token-seguro`, base `c64bb0a` de `feat/backend-swagger-ui`. Nenhum arquivo de Front alterado. Sem remoto configurado, sem PR publicada ou merge.

## Entregas verificadas

- G-US-01: configuração tipada/lazy, validação sem valores, transporte sem redirecionamento/cookies e ausência de logs HTTP.
- G-US-02: OAuth com form body e endpoint fixo, DTO validado, expiração e falhas sanitizadas.
- G-US-03: singleton em memória, single-flight, expiração com relógio injetável, cancelamento isolado, recuperação limitada e proteção contra 401 atrasado.

Detalhes e configuração: [IGDB-AUTH.md](IGDB-AUTH.md).

## Validação

249 testes Release aprovados, sem ignorados; build com analisadores sem avisos/erros. Saída isolada em bin/IgdbValidation para preservar a API que já estava em execução. 45 testes novos com transporte fake e dados sintéticos, sem rede externa.

Commits de testes antecedem as implementações: `a39b064` (configuração, 9 falhas RED), `6e99d9d` (OAuth, 18 falhas RED), `041958f` (cache/401, 13 falhas RED) e `b9251f2` (tradução segura, 503 esperado versus 500 no RED).

A antiga restrição de ausência de HTTP em Infrastructure foi ajustada para a quarta fase. Domain/Application continuam sem referência HTTP; o fake continua construível sem rede e lookups atuais não ativam integração externa. Não foi adicionada dependência de banco, ORM, migrations ou persistência.

## Pendências explícitas

Estado: IMPLEMENTED_LOCAL_DEPENDENCY_PENDING. A terceira fase ainda não consta validada integralmente no planejamento; esta infraestrutura foi iniciada por solicitação do usuário, sem ativar catálogo externo ou declarar essa dependência concluída.

Atualização na G-PR-02 em 2026-10-14: a dependência da terceira fase foi conferida na entrega I-PR-07 de Front (`0981a79`), com runtime HTTP e evidências E2E versionadas. O estado acima descreve o encerramento original da G-PR-01; a aquisição live e a ativação do catálogo continuam pendentes.

Não houve aquisição live de token ou consumo real da IGDB. Cache de catálogo, política cache/local/IGDB, consultas, paginação e tradução de jogos pertencem às próximas PRs. O catálogo atual permanece fake. A implementação local não comprova a conclusão da quarta fase.
