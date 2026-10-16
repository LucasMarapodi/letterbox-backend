# Acesso HTTP pelo navegador

A política `BrowserClient` permite somente `Cors:AllowedOrigins`. Em Production, a lista padrão é vazia. Em Development, `appsettings.Development.json` permite `http://localhost:5173` e `http://127.0.0.1:5173`, origens distintas do Vite padrão. Se o Vite mudar de porta, configure a origem efetiva antes de validar.

Exemplo de configuração para um ambiente:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://web.example"]
  }
}
```

Use origens HTTP(S) completas, sem barra final, caminho, query, credenciais ou wildcard. Configuração inválida interrompe a criação da política. Variáveis de ambiente usam índices, por exemplo `Cors__AllowedOrigins__0`. Arrays de provedores de configuração são combinados por índice; para substituir uma lista de desenvolvimento, atualize todos os índices ou use um ambiente com lista padrão vazia.

Métodos permitidos: GET, POST, PATCH, PUT e DELETE. Headers permitidos: Content-Type e Authorization. Location é exposto para permitir leitura das URLs retornadas por cadastro, reviews e comentários. OPTIONS é tratado pelo middleware como preflight, sem exigir sessão. O cliente usa Bearer e não deve enviar `credentials: include`; não há `Access-Control-Allow-Credentials`.

O pipeline aplica CORS depois do roteamento e antes de autenticação/autorização. Respostas de sucesso e erro, inclusive 401 e 500, preservam os headers para origens permitidas. Origens não configuradas não recebem liberação. CORS controla a leitura pelo navegador; a API continua aplicando autorização às operações privadas e clientes HTTP de servidor continuam acessando rotas públicas.

## Execução local

Na pasta Back:

```powershell
dotnet run --project src/Tcc.Api --launch-profile http --urls http://localhost:5080
```

O perfil http define Development. Front pode executar `npm run dev -- --host localhost --port 5173 --strictPort`. A futura configuração de URL do cliente deve apontar diretamente para `http://localhost:5080`, preservando origens distintas e sem proxy.

## Evidência desta PR

`tests/Tcc.Api.Tests/Validation/CorsTests.cs` verifica sucesso, erros 400/401/403/404/409/500, origens rejeitadas, configuração customizada, defaults Development, Production sem origens, configuração inválida, Location e preflights GET/POST/PATCH/PUT/DELETE. O RED inicial apresentou 14 falhas por ausência de headers e OPTIONS 405; o commit dos testes antecede o middleware.

São testes HTTP em WebApplicationFactory, não chamadas observadas na interface. A validação final em navegador com Front consumindo Back permanece nas próximas PRs da terceira fase.
