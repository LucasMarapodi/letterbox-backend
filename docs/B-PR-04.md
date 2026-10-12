# Interações sociais e notificações

A API passa a permitir seguir usuários, favoritar jogos/reviews e receber notificações quando alguém seguido publica uma review ou outra pessoa comenta na própria review. Consultas de conexões são públicas; favoritos, estado de seguimento e notificações pertencem à identidade do Bearer.

Branch `feat/back-pr-04-interacoes-sociais`, baseada em `e6365b0` da PR-3. Repositório exclusivo em Back; nenhum merge realizado.

## Entregas

- B-US-15: relações direcionais, PUT/DELETE idempotentes, unicidade sob concorrência, rejeição de seguir a própria conta e listas públicas sem credenciais.
- B-US-16: favoritos separados por game/review, validação de tipo/alvo, IDs preservados nas repetições, listas pessoais e isolamento entre usuários.
- B-US-17: notificações para seguidores atuais e autor da review; sem auto-notificação, eventos de edição ou eventos de operações rejeitadas. Deduplicação por evento/destinatário.
- B-US-18: listagem privada completa por data decrescente/ID crescente, ator público, links identificáveis pelo reviewId e commentId apenas quando aplicável.

## Arquitetura e atomicidade

Domain protege invariantes de seguimento, tipos de favorito e formato dos eventos. Application coordena sessão, validação de alvos, destinatários e publicações. Infrastructure usa stores por host e escopo de publicação com lock compartilhado e undo das mutações. Falha na gravação de notificações desfaz review/comentário e eventos parciais antes de liberar leitores. Não há broker, serviços externos ou infraestrutura distribuída.

## Validação

As quatro US tiveram RED executado e commit dos testes antes da implementação. A geração inicialmente não produzia eventos; um teste adicional de falha revelou a necessidade de rollback e foi commitado antes da correção.

Em 12/10/2026, SDK 10.0.401:

- `dotnet test --no-restore`: 130 testes aprovados, sem ignorados (110 Api, 19 Domain, 1 Application).
- `dotnet build --no-restore -c Release`: sucesso, zero avisos e erros, com nullable e análises habilitados.
- `git diff --check`: sem erros. Testes cobrem concorrência, direção das relações, isolamento de favoritos/eventos, logout/expiração das publicações, deduplicação, rollback e novo host sem dados sociais anteriores.

## Escopo e publicação

Front preservado. Sem notificações manuais, leitura/read, e-mail, likes, respostas encadeadas, banco ou IGDB real. Dashboard, feeds e rankings ficam na PR-5. Contratos e exemplos: [SOCIAL.md](SOCIAL.md).

Sem remoto configurado: a PR remota não foi aberta. Falta definir remoto e branch base de destino para publicação. A base local utilizada é a entrega anterior; nenhum merge realizado.
