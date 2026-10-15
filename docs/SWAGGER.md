# Swagger UI

Execute na pasta Back:

```powershell
dotnet restore
dotnet run --project src/Tcc.Api --launch-profile http --urls http://localhost:5080
```

Abra [Swagger local](http://localhost:5080/swagger). Se utilizar outra porta, abra `/swagger` na mesma origem da API. A página fica disponível em todos os ambientes, assim como o documento OpenAPI já existente.

Os perfis http/https em `launchSettings.json` usam `launchUrl: swagger` para abrir essa página ao iniciar o projeto por uma IDE que respeite o perfil. Sem a opção `--urls`, o perfil http utiliza `http://localhost:5207/swagger`.

## Explorar e executar operações

O Swagger UI carrega `/openapi/v1.json`, gerado pelos controllers e DTOs, com os contratos, exemplos, status e requisitos Bearer atuais. O inventário contém 34 operações funcionais e health. Os arquivos JavaScript/CSS são servidos pelo próprio backend a partir do pacote, sem CDN. Não existe uma segunda geração de contratos por SwaggerGen.

Para chamadas públicas, selecione a operação, clique em **Try it out**, preencha os parâmetros e clique em **Execute**.

Para chamadas privadas:

1. Execute `POST /api/v1/auth/login` com uma conta local. A fixture de desenvolvimento está descrita no README.
2. Copie somente o valor de `token` da resposta.
3. Clique em **Authorize**, cole o token sem o prefixo Bearer e confirme.
4. Execute as operações privadas. A interface monta o header Bearer exigido pelo contrato.
5. Ao encerrar, execute `POST /api/v1/auth/logout` para revogar a sessão e remova a autorização no Swagger.

A autorização da interface não é persistida após recarregar a página. Remover o token no botão Authorize apenas limpa a interface; a revogação exige logout. Desde P-PR-01, reiniciar preserva usuários, perfis e sessões no PostgreSQL; conteúdo e relações ainda são transitórios. Registre sua conta antes do login: não há seed de usuários no runtime. Escritas pelo Swagger usam os mesmos endpoints e alteram o estado visto pelos outros clientes.

## Manutenção

Dependência exclusiva da camada Api: `Swashbuckle.AspNetCore.SwaggerUI` 10.3.0. A URL do documento é relativa (`../openapi/v1.json`), evitando fixar hostname/porta. O gerador permanece `Microsoft.AspNetCore.OpenApi` e os transformers em `OpenApiRegistration.cs`.

Testes em `tests/Tcc.Api.Tests/Validation/SwaggerUiTests.cs` verificam página e inicializador em Development/Production, documento configurado, assets locais, autorização sem persistência e redirecionamento de `/swagger`. `OpenApiTests` continua verificando o inventário e a segurança por operação.

Entrega na branch `feat/backend-swagger-ui`, baseada em `a1d3d8b` da primeira PR de integração. RED com 6 falhas por rotas ausentes, commitado antes da implementação; validação final com 204 testes Release aprovados e build com analisadores sem erros ou avisos. Não houve alteração de Front ou publicação de PR remota.

Referência: [documentação oficial do Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/configure-and-customize-swaggerui.md).
