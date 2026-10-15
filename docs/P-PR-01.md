# P-PR-01 — Identidade persistida em PostgreSQL

Cadastro, login, perfil e sessões antes dependiam da memória do host e eram perdidos ao reiniciar. Agora contas/perfis/sessões usam PostgreSQL; o mesmo token continua válido após reiniciar a API e logout revoga somente essa sessão.

- EF Core 10/Npgsql, DbContext scoped, factory de migrations sem startup da API e configuração por ambiente/`.env` ignorado em Development.
- `usuarios`, `perfis_usuarios` owned e `sessoes_usuarios`; IDs integer gerados pelo banco, FKs RESTRICT, unicidade de email/username/token_hash e timestamps UTC.
- Cadastro atômico com 409 em disputa por email; PATCH serializa alterações parciais com bloqueio de linha. Queries por ID/email/username e projeções de autores em lote são assíncronas.
- Token aleatório de 256 bits; banco guarda SHA-256, expiração e revogação. Sem fontes de identidade ou seeds em memória no runtime. SQL indisponível retorna erro seguro 503/storage_unavailable.
- Contratos de usuário passam a JSON number. Referências de usuário foram adaptadas nas features existentes, mantendo SQL fora dos locks síncronos de publicação.

Validação em PostgreSQL 17 real isolado: migrations, materialização, unicidade concorrente, rollback conta/perfil, PATCH concorrente e reinício, token hash, expiração, logout isolado e integridade de FK. Regressões HTTP de autenticação/opiniões/social/feeds/catálogo e segurança também executadas. Suíte completa: 386 testes aprovados (26 Domain, 1 Application, 359 API) e 1 smoke externo opt-in ignorado. O teste adicional de sessões de duas contas passou na execução direcionada (4/4). Build Release com analisadores: zero avisos/erros. Testes SQL exigem configuração explícita e não são substituídos por EF InMemory.

Commits RED precedem implementação de infraestrutura, modelo/materialização, cadastro, sessões e indisponibilidade SQL. Schema de desenvolvimento `gamesdb` atualizado sem drop, e testes isolados em `tcc_pr01_tests`. [Configuração e operação](POSTGRES.md).

Limites: sem alteração de Front; adaptação do consumidor aos IDs de usuário numéricos é dependência de liberação. Outras tabelas/IDs seguem em transição; catálogo local ainda está em memória até P-PR-02. Sem remoto Git configurado, esta descrição prepara revisão local, sem publicação ou merge.
