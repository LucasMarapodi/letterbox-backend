# Contratos e validação final do backend

O backend das cinco entregas anteriores passa a ter documentação OpenAPI gerada dos DTOs e validação consolidada de acesso/estado. A entrega permanece exclusivamente local em Back; o Front continua usando mocks.

Branch `feat/back-pr-06-validacao`, baseada em `fd36577` da PR-5. Sem merge ou PR remota.

## Entregas

- B-US-24: OpenAPI 3.1.1 em GET /openapi/v1.json, com as 34 operações funcionais e health, schemas nomeados, erros ApiError, exemplos fictícios, datas ISO, períodos/ordenações e Bearer nas operações privadas. Requests com senha são writeOnly; DTOs públicos não expõem credenciais. Status 201/204 e respostas 200 são explícitos nas operações.
- B-US-25: matriz HTTP cobre todas as operações privadas com sessão ausente/falsa/expirada/revogada e todas as leituras públicas sem token. Fluxo integrado cobre cadastro/login/perfil, opiniões, seguir, favoritos/dashboard, notificações, rankings e novo host. Concorrência de substituição de nota mantém um ID/voto local. Esses sete testes passaram sobre o comportamento existente, sem correções funcionais ou RED artificial.
- B-US-26: documentação de execução/limitações, revisão das entregas anteriores, validação Release com análises, diff restrito ao backend e descrição final preparada. A revisão do artefato OpenAPI gerou um RED específico para servidor relativo, seguido da correção e nova execução completa.

## Validação executada

Em 12/10/2026, SDK 10.0.401:

- `dotnet test --no-restore -c Release`: 167 testes aprovados, sem ignorados (140 Api, 26 Domain, 1 Application).
- `dotnet build --no-restore -c Release -p:RunAnalyzers=true`: sucesso, zero avisos e erros; nullable e warnings as errors ativos.
- Documento exportado de uma instância Release executada temporariamente: OpenAPI 3.1.1, 35 operações, servidor relativo `/`. O processo iniciado para essa verificação foi encerrado.
- `git diff fd36577 --check`: sem erros. Mudanças de implementação/documentação/testes dentro do repositório Back; sem pacotes ou código de Front.
- PR-1 a PR-5 estavam READY_LOCAL com critérios marcados; a suíte final reexecuta suas regressões, incluindo autoria, concorrência, rollback e isolamento por host.

## Artefatos para revisão

- [Execução e teste](../README.md)
- [Documento OpenAPI exportado](openapi.json)
- [Convenções HTTP](CONTRACTS.md)
- [Mapeamento para contratos TypeScript](COMPATIBILITY.md)
- [Identidade](AUTH.md), [opiniões](OPINIONS.md), [social](SOCIAL.md) e [feeds/rankings](FEEDS.md)

## Limitações e publicação

Dados e sessões existem somente em memória, com fixtures por host. Token opaco simulado, sem JWT/autenticação de produção; IGDB fake, sem chamadas externas ou credenciais. Sem SQL, broker, likes, respostas encadeadas, leitura/read de notificações, e-mail ou adaptação HTTP do Front.

As seis entregas do planejamento backend estão implementadas e verificadas localmente. As PRs remotas ainda dependem de remoto e branch base de destino configurados; não há remoto em Back, e nenhuma publicação/merge foi declarada como realizada.
