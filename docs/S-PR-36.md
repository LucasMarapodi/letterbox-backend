# S-PR-36 — Título de review, validação e prioridade na API

Parte Back da S-US-30, branch `feat/sexta-36-titulo-review-api`, base `main` (`9434297`). Pré-requisito S-US-20 atendido pela integração unificada de review/nota (S-PR-21, PR retrospectiva #25). Commit RED `631cff6` anterior ao código: sete falhas funcionais, duas de domínio e cinco HTTP/SQL/OpenAPI.

## Conteúdo e contratos

POST de review aceita `title` nullable/opcional, normalizado por trim e limitado a 150 caracteres após trim. Nota continua obrigatória, finita entre 0 e 5, com zero válido. Somente nota é válido; título sem descrição também. Descrição preenchida exige título. Texto conserva o limite de 10.000 caracteres após trim.

PATCH distingue title omitido (preserva) de null/vazio/espaços (solicita remoção). Valida o estado final junto da descrição: remover título mantendo texto retorna 400/review_title_required; exceder 150 retorna 400/review_title_too_long. Limpar título e descrição juntos transforma a review em somente nota sem excluir ou recriar sua identidade. Outros erros de conteúdo/nota mantêm validation_failed; autoria e sessão continuam 403/401.

`ReviewResponse` retorna title explicitamente nullable em detalhes, consulta própria, opiniões/jogo, perfil, favoritos, feed, dashboard e Comunidade. Título da review é independente de `game.title`. ReviewCreateRequest/ReviewPatchRequest, indicadores de campo informado e OpenAPI foram atualizados.

Review valida a regra no domínio; casos de uso validam antes de escrever, e PostgresReviews repete a validação do estado corrente após adquirir o lock de usuário. POST/PATCH/PUT legado de nota mantêm a transação de review/avaliação. PUT de nota preserva título/texto e não contorna a validação do legado textual sem título. IDs, createdAt, escala/média da nota, autoria, unicidade, comentários, likes e favoritos permanecem preservados; edição não emite nova notificação de publicação.

## Prioridade e projeções

ReviewOrdering aplica, às coleções completas, título preenchido primeiro, CreatedAt decrescente e ID crescente. PostgresReviews.QueryAsync, favoritos SQL e PostgresFollows.FeedAsync aplicam a política no PostgreSQL; favoritos projetados usam a mesma política. As listas atuais de reviews não têm paginação no servidor: a ordem completa chega correta antes do slice do perfil. Nenhuma review sem título é removida.

QueryReviews.ForGame e PersonalDashboard não reordenam por data depois da consulta; ReviewProjection preserva a sequência recebida. Mais comentadas mantém CommentCount como primeiro critério, usando título somente no empate, antes de LastCommentAt/ID. Ranking de jogos mantém seus desempates e nenhuma regra de ranking de comentários foi alterada.

## Banco e histórico

Migration `20261016221305_ReviewTitle`: somente `ALTER TABLE reviews ADD titulo character varying(150)`. Coluna nullable, sem default, backfill artificial, tabela nova, índice ou mudança de PK/FK/unicidade. As relações de review com usuário, jogo, avaliação e interações continuam iguais. Não há CHECK global texto→título, permitindo materializar e ler o histórico.

Legados textuais sem título retornam title=null e ficam no grupo sem título. Ao editar mantendo descrição, o autor deve fornecer título; se a nota legada também faltar, deve regularizar ambos. A construção EF/materialização não aplica validação de nova gravação sobre dados históricos. Down remove apenas a coluna e perde seus valores, mantendo reviews/vínculos; não é necessário para atualizar.

Up/Down e a auditoria da cadeia foram exercitados somente em `tcc_pr01_tests`, com guarda de host/nome/marcador. O snapshot e o novo Designer preservam a configuração RESTRICT do perfil owned, que o gerador EF omite. Nenhuma migration ou escrita foi executada em `gamesdb`: aplicar o upgrade explicitamente antes de iniciar o novo backend, conforme POSTGRES.md. Startup continua sem migrations automáticas.

## Validação e consumidor

Os sete testes novos passaram: matriz de título/texto/nota zero, trim, limite 150/151, PATCH omitido/limpeza, erro sem gravação, concorrência título/nota, PUT legado, reinício, preservação de identidade/média/interações, DTOs de todas as projeções, prioridade e desempates, métrica principal do ranking e upgrade de histórico sem título. Fixtures válidas anteriores receberam títulos explícitos de teste; não há geração automática de título no runtime.

Verificação final: **556 testes aprovados** (501 API, 54 Domain e um Application), somente smoke live IGDB opt-in ignorado. Build Release/analisadores com zero avisos e erros; `git diff --check` aprovado.

O teste de upgrade antigo cria sua relação de comentário sem consultar uma entidade Review do modelo novo enquanto a coluna ainda não existe. A auditoria da cadeia espera 11 migrations; os testes de contratos verificam também title, mantendo as demais verificações.

Health, Swagger e OpenAPI retornaram 200 em API temporária encerrada após exportar o documento. Saída de validação isolada em `bin/SPr36/`, sem encerrar a API habitual. Smoke externo IGDB permanece opt-in.

**Consumo Front:** não houve alteração no frontend, pacotes ou modelos fora do escopo. A S-PR-37 deve enviar title quando houver descrição, tratar os códigos específicos e renderizar o título nullable, preservando a ordem da API. O formulário antigo que envia texto sem título receberá 400 após a ativação do novo contrato. Somente nota continua válido. A US completa continua pendente da parte Front.
