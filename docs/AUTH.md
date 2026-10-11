# Contratos de identidade e perfil — B-PR-02

Base `/api/v1`. IDs em string opaca. Todos os erros usam ApiError da fundação.

| Operação | Request | Sucesso |
| --- | --- | --- |
| POST /auth/register | name, email, password | 201, PublicUserResponse e Location /api/v1/users/{id}; sem sessão automática |
| POST /auth/login | email, password | 200, token, expiresAt UTC e user |
| GET /auth/me | Bearer | 200, PublicUserResponse |
| POST /auth/logout | Bearer | 204, revoga somente este token |
| GET /users/{userId} | público | 200, PublicUserResponse; 404 se inexistente |
| PATCH /users/me/profile | Bearer; displayName, bio, avatarUrl opcionais | 200, PublicUserResponse atualizado |

PublicUserResponse: id, username, displayName, bio, avatarUrl. Sem email, password, hash ou token. Username gerado como `user-{id}`: função determinística do ID único da conta; não é solicitado no cadastro.

Nome/name/displayName: obrigatório, não vazio após trim, máximo 100 caracteres. Email: obrigatório, formato simples de caixa postal sem nome de exibição, máximo 254 caracteres, trim e lowercase para unicidade. Password: obrigatório e não apenas espaços; preservado exatamente, sem exigir tamanho mínimo ou classes de caracteres adicionais. Bio: máximo 2000 caracteres, string vazia permitida. Avatar: null ou URL absoluta HTTP/HTTPS de até 2048 caracteres; não há download ou upload.

PATCH: campo omitido preserva o valor; avatarUrl null limpa foto. displayName e bio explícitos como null são inválidos. Campos desconhecidos (incluindo userId, username, email e password) são rejeitados com 400. Todas as propriedades são validadas antes da gravação. Um objeto vazio é um comando válido sem alterações.

Email duplicado retorna 409/conflict; payload inválido 400/validation_failed; credenciais incorretas e Bearer ausente, inválido ou expirado 401/unauthorized. Expiração quando agora >= expiresAt. `Authentication:SessionMinutes` configura duração, padrão 60, deve ser positivo. Cada login gera token opaco aleatório próprio. Logout e reinício invalidam sessões; logout não invalida outros tokens da mesma conta.

Fixtures por host: Ana, user-1, ana@example.com / teste123 (credenciais fictícias de desenvolvimento compatíveis com o mock). Senha armazenada somente como verificador com salt. Cadastro e alterações não sobrevivem ao reinício.

O verificador usa PBKDF2-SHA512, 210.000 iterações, salt aleatório de 16 bytes e resultado de 32 bytes; comparação com FixedTimeEquals. Tokens usam 32 bytes aleatórios representados em hexadecimal. Segredos permanecem no store interno; somente o login entrega o token correspondente.

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

Mapeamento frontend, por leitura: RegistrationInput mantém name/email/password e PublicUserResponse mantém User. LoginResponse acrescenta token/expiresAt necessários ao futuro adaptador HTTP; ele deverá guardar token e retornar user ao AuthService. Os códigos HTTP precisam ser convertidos para invalid-credentials, duplicate-email e invalid-fields. Nenhum adaptador ou código do Front será modificado nesta PR.

Implementação de autenticação usará o [mecanismo de handlers do ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/) e hashing pela [API PBKDF2 do .NET](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2?view=net-10.0).
