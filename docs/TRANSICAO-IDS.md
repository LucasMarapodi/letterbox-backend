# IDs inteiros — decisão da quinta fase

## Regra definitiva

| Uso | .NET | PostgreSQL | Geração |
| --- | --- | --- | --- |
| ID local próprio | int | integer | Identity do banco |
| FK obrigatória | int | integer | Recebe o ID do registro relacionado |
| FK opcional | int? | integer nullable | null para ausência |
| PK composta | Par de int | Par de integer | Sem identity adicional |
| PK/FK de perfil | int | integer | Mesmo ID do usuário |
| ID externo IGDB | long | bigint | Fornecido pela IGDB, não gerado localmente |

Não usar Guid/UUID, Guid.NewGuid ou strings como identidade final de entidade. Token aleatório, hash de senha e segredo são textos/bytes de segurança, não IDs de tabelas; não convertê-los para inteiro.

## Situação real do código

Após P-PR-01, UserAccount.Id, UserSession.Id/UserId e referências de usuário são int; DTOs de usuário/autor e userId/actorId são numéricos. Username textual user-{id} é calculado pelo banco e não é PK. Após P-PR-02, Game.Id e referências GameId são int, Game.IgdbId é long/bigint único separado e Genre/Platform têm identidade própria integer e externa bigint. GameResponse inclui id local e igdbId externo; DTOs/resumos/gameId são numéricos. Após P-PR-03, Review/Rating têm IDs integer identity, referências reviewId são int e as portas de opiniões usam SQL. Comment e demais identidades próprias seguem strings até suas PRs; seus stores continuam transitórios. O targetId polimórfico de favoritos em memória permanece texto decimal para jogos e reviews até sua normalização. Não havia identidade SQL anterior para importar. O Front não foi alterado e sua adaptação é dependência de liberação.

## Implementação por tabela

- US A: alterar IDs/FKs, construtores, objetos de valor quando usados, EF mappings e testes de materialização/generation.
- US B: repositórios, portas, filtros, relações, DTOs e rotas usam os IDs numéricos. Retornar ID definitivo depois de persistir.
- Não impor ID positivo antes da persistência de uma entidade nova cujo ID ainda será atribuído pelo EF; ID público após commit deve ser positivo. Testar identidade temporária e atribuição correta.
- Eventos que apontam para review/comentário precisam do ID gerado. Coordenar gravação e criação das notificações na mesma transação sem gravar FK zero ou perder atomicidade.
- Testes/seeds devem obter IDs reais por criação persistida ou fixtures numéricas válidas com estratégia de identity revisada; não reutilizar user-1/game-1 como chaves finais.
- Se existirem dados persistidos anteriores, criar mapa de ID antigo para novo e remapear todas as FKs dentro de migração/importação controlada. Não descartar dados nem usar substring/cast de GUID para produzir o número.

## Contratos e consumidor

IDs locais em DTOs passam a JSON number. Rotas HTTP são texto no transporte, mas binding/validação da API usa inteiro positivo. O frontend deve tratar IDs como number e converter parâmetros de URL por validação explícita.

Isso altera o contrato atual com Id string e validadores TypeScript. Preparar PR própria de Front para tipos, adaptadores, rotas e testes antes de liberar API numérica; não esconder incompatibilidade usando as any ou converter para string em toda camada mantendo o modelo antigo.

As PRs de PostgreSQL continuam em Back. Adaptação mínima de tipos é uma dependência de compatibilidade, não autorização para redesenhar telas ou implementar a interface de replies/likes.

## Verificação de conclusão

- Metadados EF sem PK/FK local string/uuid.
- Classes, queries e DTOs locais com int/int?.
- Identity somente nas PKs próprias; perfil e junções usam IDs existentes.
- igdb_id é numérico e separado da PK local.
- Nenhum Guid.NewGuid para gerar identidade de entidade.
- Seeds/importações, relações e consumidores preservados com testes.
- Relatório distingue código ainda legado de regra já implementada.
