# Autenticação de aplicação Twitch/IGDB

G-PR-01 implementou a autenticação externa no servidor. Desde G-PR-04 o catálogo HTTP usa o adaptador IGDB assíncrono, somente após cache/local insuficientes; fake restrito a testes. O token Twitch é separado das sessões locais de usuários. As evidências históricas da G-PR-01 abaixo não representam smoke live.

## Configuração

Defina somente no processo que executa Back:

| Variável | Uso |
| --- | --- |
| IGDB__ClientId | Identificador da aplicação Twitch |
| IGDB__ClientSecret | Segredo da aplicação Twitch |
| IGDB__TokenRefreshMarginSeconds | Opcional: inteiro de 0 a 3600; padrão 30 segundos |

`IgdbCredentialOptions` lê diretamente essas variáveis com `EnvironmentVariableTarget.Process`; não lê credenciais de appsettings, argumentos HTTP, query, arquivos Front ou IConfiguration. Não versionar valores reais ou copiá-los para logs, comandos compartilhados ou relatórios. Valores ausentes/inválidos geram mensagem com os nomes das variáveis, sem seus conteúdos. Objetos de opções e token ocultam valores em ToString e serialização JSON.

As opções são resolvidas somente quando a integração externa é solicitada. Health, OpenAPI, Swagger, login local e consultas atendidas pelo cache/catálogo local não adquirem token nem exigem credenciais externas. Jogos/páginas desconhecidos precisam da configuração. Reinicie Back após alterar credenciais ou margem. Não existe endpoint de emissão/consulta de token Twitch.

## Fluxo interno

1. O consumidor externo pede token ao singleton `TwitchTokenProvider`.
2. Um token utilizável é reutilizado em memória. Uma aquisição pendente é compartilhada pelos solicitantes, inclusive em falha.
3. `TwitchOAuthClient` envia POST para a URL fixa HTTPS `https://id.twitch.tv/oauth2/token`, com `application/x-www-form-urlencoded`: client_id, client_secret e grant_type=client_credentials no body. Não há segredo na URL.
4. A resposta deve conter token não vazio e adequado ao header HTTP, expires_in inteiro positivo e token_type bearer. Erros HTTP, redirecionamentos, transporte, timeout e JSON inválido não produzem token.
5. Expiração é calculada a partir do instante anterior à requisição, usando TimeProvider. Uma resposta já expirada é rejeitada. A margem de renovação é limitada a metade da duração quando o token tem vida curta, evitando renovação a cada acesso.
6. `IgdbAuthenticatedClient` envia Client-ID e Bearer à URL fixa HTTPS `https://api.igdb.com/v4/games`. Ainda não é exposto em endpoint público; construção/validação de queries e mapeamento de jogos pertencem às próximas entregas.
7. Em 401 IGDB, descarta-se apenas a geração usada e tenta-se uma nova aquisição e uma repetição. Um 401 atrasado não invalida uma geração mais recente. Se a repetição também falha com 401, o token rejeitado é invalidado e a operação termina; não há loop.

Outros status da IGDB são devolvidos ao futuro adaptador de catálogo para tradução; não causam renovação de token ou retry nessa camada. Não se aplica essa repetição a reviews, comentários ou outras escritas locais.

## Transporte e erros

Dois clientes nomeados de IHttpClientFactory usam timeout de 30 segundos, TLS padrão, redirecionamentos desativados, sem cookies e sem credenciais do sistema. Os loggers HTTP desses clientes são removidos, inclusive em Trace; o código não registra bodies, headers, valores de configuração ou respostas externas. Exceções de transporte/JSON são substituídas por erro seguro sem inner exception com payload do fornecedor.

Aquisição OAuth aceita até 64 KiB de resposta no cliente configurado. O cliente IGDB possui limite de buffer de 4 MiB. O cancelamento de um solicitante interrompe sua espera; não cancela a aquisição compartilhada que outros solicitantes usam. A aquisição permanece limitada pelo timeout HTTP. Cancelamento de uma requisição IGDB é propagado, sem expor mensagens do fornecedor.

Falha persistente de autenticação externa utiliza exceção de Application, traduzida pelo pipeline para `503 {code: integration_unavailable, message: Integração externa indisponível.}`. Não é 401 da plataforma e não revoga sua sessão. Não existe dependência HTTP em Domain ou Application; OAuth/HttpClient/DTOs externos ficam em Infrastructure e a composição em Api.

## Verificação e limites

45 testes novos em `tests/Tcc.Api.Tests/Igdb`: configuração/logs, OAuth, concorrência, fronteiras de expiração, token de vida curta, cancelamento, falha compartilhada, 401 persistente/atrasado e sessão local após 503. Probes HTTP de falha existem somente no assembly de testes; não são publicados pela API produtiva.

Suíte Release: 249 aprovados (222 Api, 26 Domain, 1 Application), sem ignorados. Build com analisadores: zero erros/avisos. Uma API local estava executando e bloqueava os arquivos Release; a verificação preservou esse processo usando:

```powershell
dotnet test --no-restore -c Release -p:OutputPath=bin/IgdbValidation/
dotnet build --no-restore -c Release -p:RunAnalyzers=true -p:OutputPath=bin/IgdbValidation/
```

Sem chamada live, uso de credenciais reais ou prova de catálogo IGDB real. A ordem futura cache → catálogo local → IGDB não foi implementada aqui; testes confirmam somente que os lookups atuais não acionam rede. A terceira fase ainda consta pendente de validação final e retirada de mocks no planejamento. O início desta infraestrutura foi solicitado pelo usuário; essa dependência permanece explícita antes da ativação do catálogo externo.

Fontes oficiais: [Twitch client credentials](https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#client-credentials-grant-flow) e [autenticação IGDB](https://api-docs.igdb.com/#authentication).
