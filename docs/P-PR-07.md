# P-PR-07 — Validação final da persistência

A quinta fase conclui a validação local do backend com as 16 tabelas de negócio no PostgreSQL. A API preserva contas, perfis, sessões válidas, catálogo, conteúdo e relações ao reiniciar. Cache de catálogo, cobertura de consultas e token Twitch continuam transitórios.

## Código e funcionalidade

`FinalMigrationAuditTests` valida o schema real contra o modelo relacional de design do EF: tabelas, colunas/tipos/nullabilidade, nomes de constraints, colunas e destinos das FKs, RESTRICT, índices/colunas/unicidade/presença de filtro e identities. PKs/FKs locais são integer; igdb_id externo é bigint. A suíte anterior verifica o comportamento dos CHECKs, predicados parciais, trigger de respostas e constraints sob concorrência.

O teste reverte as sete migrations até zero exclusivamente no banco marcado de testes, confirma ausência das tabelas de negócio e reaplica a cadeia completa. `__EFMigrationsHistory` pode permanecer vazio nessa etapa. Depois retorna à migration IdentitySessions, insere a fixture sintética versionada `IdentityUpgradeBaseline` e aplica as migrations restantes. IDs, vínculos, conteúdo público e timestamps de conta/perfil/sessão permanecem iguais; o schema final volta a coincidir com o modelo e não há alteração pendente de snapshot. A baseline cobre o schema histórico de identidade; preservação dos grupos posteriores também está coberta nos testes de cada entrega.

`FinalPersistenceFlowTests` executa cadastro/login de duas contas, edição de perfil, seguir, nota, review, comentário, resposta, likes, favoritos e logout de uma sessão adicional. Descarta o primeiro host da API e cria outro com novas instâncias e o mesmo banco. Compara respostas HTTP antes/depois para as duas identidades, verifica conteúdo/relações, média 4,5, isolamento de feed/favoritos/notificações e recusa da sessão revogada. Confirma registros nas 16 tabelas. O catálogo é preparado pelo repositório SQL; fornecedor substituído por implementação que falha se chamado comprova resolução local sem IGDB.

A auditoria de DI resolve as nove portas de negócio para repositórios PostgreSQL scoped, catálogo local pelo adaptador SQL e cache compartilhado. Domain/Application continuam sem referência a EF/Npgsql. `HostMemory` legado permanece sem registro e não armazena entidades; stores em memória usados pelas regressões estão no assembly de testes.

Não foi necessária alteração de regra de negócio, endpoint, contrato ou migration. O commit inicial `ac50f56` contém os testes antes da conclusão. A primeira execução teve dois cenários aprovados e falha no próprio comparador de schema; ajustes de ordenação ordinal e conversão explícita do tipo interno PostgreSQL corrigiram o harness. Isso não é evidência RED de um defeito de produção, nem se criou falha artificial.

## Segurança da execução e evolução

Antes de qualquer reversão, o harness exige nome exato do banco local exclusivo e comentário de isolamento. Todos os testes SQL compartilham coleção sem paralelismo. O finally reaplica as migrations caso uma asserção intermediária falhe. A reconstrução remove dados sintéticos apenas desse alvo; nunca usar esse procedimento em banco compartilhado.

As operações Up existentes criam tabelas, índices, constraints, função e trigger; não foi adicionada migration para apagar/recriar dados. As operações Down removem as tabelas correspondentes e, em CommentsAndLikes, a função/trigger de hierarquia. Portanto rollback pode destruir dados da feature removida; não serve como estratégia de atualização de ambiente com conteúdo a preservar. FKs RESTRICT protegem relações em uso. Migrations continuam explícitas, sem execução no startup.

O banco de desenvolvimento foi consultado somente para conferência: 16 tabelas de negócio, sete migrations, seis contas, 43 jogos e uma review. Não houve escrita, seed, reset ou aplicação de migration nesse banco nesta PR. Credenciais e arquivos de ambiente continuam privados e ignorados.

## Verificação

Os três cenários novos passaram; os dois cenários de integração usam PostgreSQL 17 real e a auditoria de DI não abre conexão. Suíte completa com 417 testes aprovados (27 Domain, 1 Application, 389 API), um smoke live IGDB opt-in ignorado. Build Release/analisadores sem avisos ou erros. A suíte cobre falhas SQL sem fallback, rollback de publicações/eventos, invisibilidade antes do commit, unicidade concorrente e autoria.

Em instância temporária encerrada após a conferência: health, Swagger e OpenAPI responderam 200; sete schemas públicos expõem id integer, GameResponse.igdbId permanece int64 e notificações sem sessão retornam 401. Nenhum contrato novo foi introduzido; a exportação OpenAPI existente foi mantida.

Para reproduzir, prepare o alvo exclusivo descrito em [POSTGRES.md](POSTGRES.md) e execute em Back:

```powershell
dotnet test --no-restore -c Release
dotnet build --no-restore -c Release -p:RunAnalyzers=true
```

Sem `.env.postgres-test`, os cenários PostgresFact são ignorados; isso não comprova SQL. O smoke live IGDB é opt-in e não integra esta validação. A ordem cache → SQL → IGDB é coberta também por PostgresCatalogHttpTests, incluindo preenchimento/evicção de cache, restart e indisponibilidade SQL sem chamar fornecedor.

## Entrega e compatibilidade

Branch `feat/postgres-pr-07-validacao`, P-US-34–35. Descrição e [matriz final](MATRIZ-PERSISTENCIA.md) versionadas, entrega local preparada para revisão. Sem remoto configurado, não existe PR publicada nem merge.

Front não alterado. A publicação integrada continua dependendo da adaptação do consumidor aos IDs numéricos já introduzidos nas PRs anteriores. UI de respostas/likes é trabalho separado. A conclusão aqui é da implementação e validação local de persistência do backend.
