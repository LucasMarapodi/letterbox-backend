# HTTPS da API — C-PR-04

O endereço local padrão é `https://localhost:7119`. O único perfil de launchSettings é `https`, em Development, sem listener HTTP. A API também rejeita qualquer requisição cujo esquema efetivo não seja HTTPS com 400/validation_failed, antes do roteamento, CORS, autenticação e endpoints. Não utiliza redirect para login/POST/OPTIONS e não ativa HSTS no localhost.

## Certificado e execução local

Na pasta Back, confira o certificado existente sem modificá-lo:

```powershell
dotnet dev-certs https --check --trust
```

Se necessário, execute explicitamente `dotnet dev-certs https --trust` para criar/confiar no certificado de desenvolvimento, respeitando a confirmação do Windows. Não use `--clean`, não ignore erros TLS e não desative a validação no navegador/cliente. O certificado precisa estar válido, confiável e ter SAN correspondente ao hostname. Prefira localhost; não presuma que todo certificado antigo cobre outro hostname/IP.

```powershell
dotnet restore
dotnet build --no-restore -c Release
dotnet run --no-build --project src/Tcc.Api -c Release --launch-profile https
```

Sem launch profile, declare ambiente e URL explicitamente:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-build --no-launch-profile --project src/Tcc.Api -c Release --urls https://localhost:7119
```

CLI, variáveis ASPNETCORE_URLS e configuração de endpoints do Kestrel podem alterar os listeners; confira as URLs efetivamente anunciadas. Editar launchSettings não migra um processo já iniciado. O processo sem perfil não recebe automaticamente os endereços do launchSettings. Não mantenha overrides HTTP antigos: mesmo que abram um listener, a política rejeita suas chamadas.

Validação sem bypass TLS:

```powershell
Invoke-WebRequest https://localhost:7119/health
Invoke-WebRequest https://localhost:7119/swagger/index.html
Invoke-WebRequest https://localhost:7119/openapi/v1.json
```

Swagger usa documento relativo e as operações usam a mesma origem HTTPS. Cadastro/login devem usar a URL final HTTPS; um cliente que envia senha por HTTP já a expôs, mesmo quando a API rejeita a requisição. Health sozinho não comprova acesso ao banco. Esta mudança não cria banco, executa migrations nem altera PostgreSQL, sessões ou hashing.

## Front e CORS

Em Development, a única origem permitida é `https://localhost:5173`. Em Production, a lista padrão é vazia; configure `Cors__AllowedOrigins__0` e demais índices com origens HTTPS exatas, sem caminho, barra final, credenciais ou wildcard. Headers Content-Type/Authorization, métodos existentes e exposição de Location/metadados de catálogo permanecem. Preflight HTTPS não exige token nem redireciona. Não habilitar cookies/AllowCredentials.

O Front deve servir HTTPS e usar `VITE_API_BASE_URL=https://localhost:7119`, conforme C-PR-05. Configurar apenas a URL da API não habilita TLS no Vite. Certificados/chaves ficam fora do Git, de public e do bundle; caminhos TLS privados são configuração do processo, nunca variáveis VITE_*. Esta PR não altera arquivos do Front nem seu .env.

## Avatares e dados históricos

Nova URL de avatar exige HTTPS absoluto, com o limite existente de 2048 caracteres. PATCH e PUT de conclusão do perfil compartilham UserProfile.Apply: omissão preserva, null/vazio remove, valor histórico HTTP inalterado é preservado inclusive no round-trip do PUT. Outros campos permanecem editáveis. Materialização EF continua usando o construtor privado existente, sem revalidar os registros históricos. Não há conversão de HTTP para HTTPS, download/proxy de imagens ou migration. O Front tratará renderização dos valores antigos na C-US-07.

## Hospedagem futura — ainda não implantada

LaunchSettings e o certificado de desenvolvimento não configuram hospedagem pública. Domínio, provedor, topologia, emissão pública, renovação, armazenamento de chave e regras de rede continuam pendentes de escolha.

- TLS direto no Kestrel: fornecer certificado público válido por configuração segura de Kestrel:Certificates/Endpoints, segredo pelo ambiente ou cofre do host, e listener HTTPS explícito. Não incluir PFX, PEM, chave ou senha no repo.
- TLS no proxy/serviço: Front e API públicos usam HTTPS final e certificado público válido. Upstream HTTP somente em rede restrita, sem exposição pública. Antes de usar esse modo será necessário configurar ForwardedHeaders com proxies/redes conhecidos e limite de encaminhamento, antes da política de esquema/CORS/autenticação. Sem isso, o upstream HTTP recebe 400.
- Esta entrega não instala middleware de forwarded headers nem ativa confiança em X-Forwarded-Proto. Header enviado por cliente não transforma HTTP em HTTPS. Não habilitar ASPNETCORE_FORWARDEDHEADERS_ENABLED indiscriminadamente nem esvaziar listas de confiança para aceitar qualquer origem. Testes cobrem o header forjado rejeitado.
- Redirect e HSTS do site pertencem ao terminador/host que servir o build estático, após definir domínio/topologia. Não ativar preload, includeSubDomains ou HSTS duradouro no localhost ou domínio desconhecido.
- Vite server.https atende desenvolvimento; o build estático precisa de servidor/CDN com TLS e fallback de rotas SPA. VITE_API_BASE_URL é pública e incorporada no build: configurar por ambiente e reconstruir. CORS precisa da origem pública exata.

TestServer usa esquema HTTPS para verificar política/contratos e não realiza handshake TLS. A prova integrada de navegador, HMR e conteúdo misto pertence à C-US-08; não apresentar testes em memória ou preparação como deploy.

## Validação do backend — 19/11/2026

Suíte completa Release: 671 aprovados (84 Domain, 14 Application, 573 API), um smoke IGDB externo optativo ignorado. Build Release com analisadores: zero avisos e erros. Testes cobrem transporte/CORS, header de proxy forjado, criação/substituição/remoção de avatar e round-trip SQL de avatar HTTP histórico após reinício da API. Os hosts funcionais compartilham ApiFactory com BaseAddress HTTPS; não foi criado bypass da política para TestServer.

TLS real validado numa instância temporária Kestrel em `https://localhost:7119`, usando o certificado de desenvolvimento existente, válido e confiável no Windows. Health, Swagger e OpenAPI retornaram 200; preflight permitido 204 e origem indevida sem liberação; cadastro 201, login/me 200, logout 204 e sessão revogada 401. Um listener HTTP residual de teste respondeu 400 para health/cadastro/login, mesmo com X-Forwarded-Proto forjado, sem redirecionamento; cadastro posterior em HTTPS confirmou que HTTP não criou a conta. OpenAPI exportado dessa instância.

Somente o PostgreSQL exclusivo `tcc_pr01_tests`, com marcador de exclusividade verificado, recebeu dados sintéticos. Nenhuma migration nova, alteração no banco de desenvolvimento, chamada IGDB live ou mudança em arquivos do Front. A instância temporária foi encerrada ao final. O certificado existente foi confiado por `dotnet dev-certs https --trust`, sem limpar ou exportar chaves. Navegador integrado, HMR e conteúdo misto aguardam C-PR-05/06; não há deploy público.

Referências: [HTTPS em APIs ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0#api-projects), [dotnet dev-certs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs), [proxy e headers encaminhados](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
