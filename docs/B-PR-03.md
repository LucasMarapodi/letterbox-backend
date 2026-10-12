# Catálogo, notas, reviews e comentários

A API passa a fornecer catálogo IGDB fake e permitir notas pessoais, publicação/edição de reviews e comentários autenticados. As consultas são públicas, salvo ausência/estado da própria nota ou review; escritas derivam autoria exclusivamente da sessão Bearer.

Branch `feat/back-pr-03-catalogo-reviews` baseada em `37b9275` da B-PR-02, sem merge. Implementação exclusiva no repositório Back.

## Entregas

- B-US-08: busca trim/sem distinção de caixa, ordenação determinística, detalhe e nulls normalizados; adaptador sem dependência HTTP.
- B-US-09: RatingValue finito entre 0 e 5, incluindo decimais; PUT substitui a nota preservando ID, GET retorna ausência tipada.
- B-US-10: publicação única por usuário/jogo, inclusive concorrência, com autoria e data do servidor. Retorna 201/Location e pode ser consultada.
- B-US-11: filtros por jogo/autor, detalhe com projeções públicas, review própria e opiniões; média local null/zero/substituída.
- B-US-12: edição exclusiva pelo autor; preserva ID, jogo, autoria e createdAt, atualizando updatedAt. Rejeita campos protegidos e texto inválido.
- B-US-13: comentários públicos planos por data/ID, filtro, lista vazia e 404 para review inexistente.
- B-US-14: comentário autenticado, inclusive próprio, com validação de texto e alvo, rejeição de autoria forjada e bloqueio após logout/expiração.

## Arquitetura e validação

Invariantes de nota, texto e autoria ficam em Domain. Application coordena sessão, catálogo, repositórios, relógio e projeções. Infrastructure contém fixtures imutáveis e stores com locks/snapshots, sem chamadas externas. Api usa contratos nomeados separados do domínio e os erros padronizados da fundação.

Todas as sete US tiveram testes executados em RED e commit antes da implementação; rotas ausentes retornavam 404/405. Builds e testes pertinentes foram executados antes de cada commit GREEN. Regressões finais incluem identidade/perfil, domínio, concorrência e isolamento entre hosts.

Validação final em 11/10/2026, SDK 10.0.401:

- `dotnet test --no-restore`: 112 testes aprovados, sem ignorados (96 Api, 15 Domain, 1 Application).
- `dotnet build --no-restore -c Release`: sucesso, zero avisos e erros, nullable e análises habilitados.
- `git diff 37b9275 --check`: sem erros; implementação restrita a Back.

## Escopo e publicação

Sem alteração de Front, banco, JWT, IGDB real, respostas encadeadas, likes ou exclusões. Notificações não foram antecipadas: entram na B-US-17. Contratos e limites explícitos estão em [OPINIONS.md](OPINIONS.md). OpenAPI completo e adaptação HTTP do frontend permanecem nas entregas específicas do planejamento.

Não há remoto configurado, portanto nenhuma PR remota foi criada. A publicação precisa de remoto e branch base de destino; a base local é a entrega anterior. Nenhum merge realizado.
