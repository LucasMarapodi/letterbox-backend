# Autenticação e perfil em memória

A API tinha somente a fundação HTTP. Esta entrega permite cadastrar uma conta, entrar com um token opaco, consultar e encerrar a sessão e editar o próprio perfil. Perfis são públicos; escrita depende da identidade resolvida pelo token.

Branch `feat/back-pr-02-autenticacao-perfil`, baseada em `a3e0d51` da B-PR-01, sem merge. Repositório exclusivo em Back.

## Entregas

- B-US-04: conta e perfil atômicos, normalização/unicidade de e-mail sob concorrência, username gerado e senha com verificador salted; 201 sem sessão automática ou segredos.
- B-US-05: validação de credenciais, tokens aleatórios independentes e duração configurável (60 minutos por padrão).
- B-US-06: handler Bearer reutilizável; identidade derivada do store, expiração no limite exato, me e logout com revogação individual.
- B-US-07: consulta pública com 404 para ID ausente; PATCH parcial do titular, validação antes da gravação, limpeza de avatar e rejeição de campos de conta.

Invariantes ficam em Domain, coordenação em Application, stores/hash em Infrastructure, composição/autenticação/DTOs em Api. Snapshots de conta/perfil são imutáveis; atualizações parciais são aplicadas sob lock para evitar perda de campos em requests concorrentes.

## Validação e TDD

Cada US teve testes HTTP executados em RED com as rotas ausentes e commit dos testes antes da implementação. Também há testes de domínio, armazenamento de hashes/salts, configuração inválida e concorrência na edição parcial.

Validação final em 11/10/2026, SDK 10.0.401:

- `dotnet test --no-restore`: 64 testes aprovados (55 Api, 8 Domain, 1 Application), sem ignorados.
- `dotnet build --no-restore -c Release`: sucesso, zero avisos e erros, nullable e análises habilitados.
- `git diff --check`: sem erros; alterações de implementação restritas a Back.

## Limites e publicação

Estado exclusivamente em memória, fixture local de Ana e tokens simulados. Sem persistência, JWT, integração real IGDB ou alterações de Front. Compatibilidade e payloads estão em [AUTH.md](AUTH.md). OpenAPI completo e integração do frontend permanecem nas entregas específicas do planejamento.

Sem remoto configurado, a PR remota não foi aberta. É necessário definir o remoto e a branch base de destino para publicação; a base local utilizada é a entrega anterior. Nenhum merge realizado.
