# Contratos de identidade e perfil — B-PR-02

Base `/api/v1`. Desde P-PR-01, IDs de usuários são inteiros positivos gerados pelo PostgreSQL e enviados como JSON number. Todos os erros usam ApiError da fundação.

| Operação | Request | Sucesso |
| --- | --- | --- |
| POST /auth/register | name, email, password | 201, PublicUserResponse e Location /api/v1/users/{id}; sem sessão automática |
| POST /auth/login | email, password | 200, token, expiresAt UTC e user |
| GET /auth/me | Bearer | 200, PublicUserResponse |
| POST /auth/logout | Bearer | 204, revoga somente este token |
| GET /users/{userId} | público | 200, PublicUserResponse; 404 se inexistente |
| PATCH /users/me/profile | Bearer; displayName, country, state, bio, avatarUrl, favoriteGameIds opcionais | 200, ProfileUpdateResponse: campos anteriores do usuário na raiz + completion privado |

PublicUserResponse: id, username, displayName, bio, avatarUrl. Sem email, password, hash ou token. Username gerado como `user-{id}`: função determinística do ID único da conta; não é solicitado no cadastro.

Nome/name/displayName: obrigatório, não vazio após trim, máximo 100 caracteres. Email: obrigatório, formato simples de caixa postal sem nome de exibição, máximo 254 caracteres, trim e lowercase para unicidade. Password: obrigatório e não apenas espaços; preservado exatamente, sem exigir tamanho mínimo ou classes de caracteres adicionais. Bio: máximo 2000 caracteres, string vazia permitida. Avatar: null ou URL absoluta HTTP/HTTPS de até 2048 caracteres; não há download ou upload.

PATCH: campo omitido preserva o valor; opcionais null/vazio limpam, nome null/vazio é inválido. favoriteGameIds:null é inválido, [] limpa jogos favoritos. País/estado alterados devem formar um par das opções configuradas, com estado vazio permitido; legado inalterado é preservado. Perfil incompleto pode ser salvo; todos os campos/favoritos compartilham a transação. Campos desconhecidos (incluindo userId, username, email e password) são rejeitados com 400. Um objeto vazio é válido sem alterações. [Contrato, resposta privada e opções S-PR-32](S-PR-32.md).

Email duplicado retorna 409/conflict; payload inválido 400/validation_failed; credenciais incorretas e Bearer ausente, inválido ou expirado 401/unauthorized. Falha SQL retorna 503/storage_unavailable, sem fallback. Expiração quando agora >= expiresAt. `Authentication:SessionMinutes` configura duração, padrão 60, deve ser positivo. Cada login gera token opaco aleatório próprio. Logout revoga apenas a sessão atual. Reiniciar a API preserva contas, perfis e sessões no mesmo banco.

Não há conta pré-cadastrada no runtime. Registre uma conta pelo Swagger antes do login. Ana existe apenas nas fixtures do assembly de testes. Cadastro e perfil são gravados na mesma transação; PATCH serializa alterações parciais da mesma conta por bloqueio de linha.

O verificador de senha usa PBKDF2-SHA512, 210.000 iterações, salt aleatório de 16 bytes e resultado de 32 bytes; comparação com FixedTimeEquals. Tokens usam 32 bytes aleatórios representados em hexadecimal. SessoesUsuarios guarda somente SHA-256 do token, com índice único, expiração e revogação. Somente o login entrega o token original. Configuração e migrations em [POSTGRES.md](POSTGRES.md).

## Exemplo local

```http
POST /api/v1/auth/register
Content-Type: application/json

{"name":"Lucas","email":"lucas@example.com","password":"exemplo"}
```

```http
POST /api/v1/auth/login
Content-Type: application/json

{"email":"lucas@example.com","password":"exemplo"}
```

Copie token da resposta para o cabeçalho dos comandos privados:

```http
PATCH /api/v1/users/me/profile
Authorization: Bearer <token>
Content-Type: application/json

{"displayName":"Lucas","bio":"Meus jogos favoritos","avatarUrl":null}
```

## Compatibilidade

Na S-PR-16, GET/PUT autenticados `/api/v1/users/me/profile-completion` consultam/salvam país, estado, bio, avatar e conjunto completo de jogos favoritos. A conclusão exige localização, bio e pelo menos três jogos distintos existentes; avatar é opcional. PATCH de perfil também aceita country/state opcionais: omitir preserva, null/vazio limpa. País/estado não são adicionados à resposta pública, inclusive auth/me. [Contrato, transação e exemplos](S-PR-16.md).

Mapeamento frontend, por leitura: RegistrationInput mantém name/email/password e PublicUserResponse mantém User. LoginResponse acrescenta token/expiresAt necessários ao futuro adaptador HTTP; ele deverá guardar token e retornar user ao AuthService. Os códigos HTTP precisam ser convertidos para invalid-credentials, duplicate-email e invalid-fields. Nenhum adaptador ou código do Front será modificado nesta PR.

Implementação de autenticação usará o [mecanismo de handlers do ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/) e hashing pela [API PBKDF2 do .NET](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2?view=net-10.0).
