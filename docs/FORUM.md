# Fórum por jogo — contrato da F-PR-01

Discussões pertencem diretamente a um jogo, com título e mensagem inicial; respostas formam uma lista cronológica sem aninhamento. O fórum é independente de reviews, notas e comentários de review. Mensagem inicial não é uma resposta. Um usuário pode abrir vários tópicos por jogo.

## Endpoints

| Método e rota | Acesso | Resposta |
| --- | --- | --- |
| GET /api/v1/games/{gameId}/forum/topics?page=1 | Público | 200, página de TopicSummary |
| POST /api/v1/games/{gameId}/forum/topics | Bearer | 201, TopicDetail; Location /api/v1/forum/topics/{id} |
| GET /api/v1/forum/topics/{topicId} | Público | 200, TopicDetail |
| GET /api/v1/forum/topics/{topicId}/replies?page=1 | Público | 200, página de Reply |
| POST /api/v1/forum/topics/{topicId}/replies | Bearer | 201, Reply; Location da coleção de respostas |

Criar tópico recebe `{ "title": "Dicas para começar", "text": "Como vocês iniciaram o jogo?" }`; responder recebe `{ "text": "Minha primeira dica..." }`. Campos extras são rejeitados, incluindo userId, reviewId, rating e parentReplyId. Autor vem somente da sessão. Título obrigatório com 1–150 caracteres e mensagens com 1–10.000, após trim. Texto é armazenado e transmitido como string JSON; o consumidor deve renderizar como texto, sem interpretar HTML.

Página: `{ items, page, pageSize: 20, hasNextPage }`. page é inteiro >=1 e offset deve caber em int32; não há parâmetro de tamanho nesta versão. SQL busca 21 linhas para identificar próxima página, retornando no máximo 20. Jogo existente sem tópicos ou tópico sem respostas retorna lista vazia. Alvo inexistente retorna 404; entrada inválida 400; escrita sem sessão válida 401; indisponibilidade de persistência 503 seguro.

TopicSummary: `{id,gameId,title,author,createdAt,replyCount,lastActivityAt}`. TopicDetail acrescenta `text` e `game:{id,title,coverUrl}`. Reply: `{id,topicId,author,text,createdAt}`. Author: `{id,username,displayName,avatarUrl}`; nenhum e-mail, hash, sessão ou localização. IDs locais int e datas UTC. GETs no-store.

Tópicos ordenados por lastActivityAt DESC e ID DESC; lastActivityAt é o maior instante entre criação e respostas, com replyCount agregado, sem contador persistido. Respostas por createdAt ASC e ID ASC. Ordenação e paginação ocorrem no PostgreSQL, com autores/contexto projetados em joins, sem leitura por item.

## Persistência e escrita

Migration `20261021230648_ForumTopicsAndReplies` cria apenas `topicos_forum` e `respostas_forum`, com PKs integer identity, FKs Restrict para usuários/jogos/tópicos, checks de conteúdo e índices de contexto/cronologia. Não há ParentReplyId, GameId redundante na resposta ou relacionamento com reviews. O modelo passa a 19 tabelas e 12 migrations; schema anterior permanece intacto.

Entidades/invariantes em Domain/Forum, portas/projeções/casos de uso em Application/Forum, SQL em PostgresForum, composição em ForumRegistration e cinco operações em ForumController. SessionAccess é revalidado antes da inserção. A existência de jogo/tópico é conferida antes do SaveChanges; nenhuma chamada IGDB ocorre dentro da transação do insert. Cada POST persiste uma entidade em SaveChanges atômico e retorna a projeção SQL confirmada. Não há retry automático ou promessa de idempotência; cancelar após um commit pode deixar resultado incerto e o cliente deve reconsultar.

Sem edição, remoção, moderação, curtidas, favoritos, anexos ou notificações específicas nesta entrega. Não há store de negócio em memória; fixtures existem somente nos testes de contrato/permissão.

## Ambiente e validação

Migration aplicada apenas no banco exclusivo de testes, com nome e marcador verificados pelo harness; gamesdb não recebeu a migration nesta tarefa. A API local habitual não foi reiniciada. Antes de ativar esta versão no ambiente de desenvolvimento, aplicar explicitamente a migration conforme [POSTGRES.md](POSTGRES.md). Produção não executa migrations automaticamente.

RED do modelo identificou ausência das entidades; a primeira tentativa SQL encontrou Docker desligado, registrado como falha de ambiente. Docker/contêiner existentes foram iniciados e a verificação SQL seguinte passou. REDs dos GETs retornaram 404 e dos POSTs 405 antes dos respectivos casos de uso. Commits dos testes precedem implementação por US. Os testes SQL/HTTP cobrem integridade, materialização/reabertura, isolamento entre jogos/tópicos, 21 tópicos e respostas, ordem/desempates, paginação, autor público, 400/401/404/503, sessão revogada, conteúdo/limites/campos extras, concorrência, cancelamento prévio e ausência de efeitos em reviews/notificações.

Comandos de validação:

Resultado final: 604 testes aprovados (545 API, 58 Domain, uma Application), um smoke IGDB live opt-in ignorado. Build Release/analisadores sem avisos/erros; health/Swagger/OpenAPI 200 em instância temporária e documento exportado atualizado. Auditoria de 12 migrations e 19 tabelas aprovada, incluindo Down/Up, upgrade com histórico preservado e HasPendingModelChanges=false. O fluxo de duas contas existente também cria tópico/resposta e compara leituras após reiniciar o host; todas as tabelas continuam sendo verificadas, sem excluir as novas da auditoria.

```powershell
dotnet test --no-restore -c Release -p:OutputPath=bin/Forum/
dotnet build --no-restore -c Release -p:OutputPath=bin/Forum/ -p:RunAnalyzers=true
git diff --check
```

Front e fluxo visual ficam para F-PR-02. Testes SQL/HTTP do backend não equivalem à validação integrada do navegador.
