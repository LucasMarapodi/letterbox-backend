# S-PR-07 — URLs externas configuráveis

Os endereços de consulta IGDB, capas e token Twitch agora são definidos por `ExternalEndpointOptions`, compartilhadas pelos clientes e pelo mapper. Alterar o destino não exige editar clientes HTTP nem repetir hosts no mapeamento de imagens.

## Configuração

| Variável do backend | Chave de configuração | Padrão público |
| --- | --- | --- |
| IGDB__BaseUrl | IGDB:BaseUrl | https://api.igdb.com/v4/ |
| IGDB__ImagesBaseUrl | IGDB:ImagesBaseUrl | https://images.igdb.com/igdb/image/upload/ |
| Twitch__TokenEndpoint | Twitch:TokenEndpoint | https://id.twitch.tv/oauth2/token |

Em Development, configure essas variáveis no `.env` privado da raiz Back e reinicie a API. Precedência: variável do processo, `.env` local, IConfiguration e padrão tipado. Em outros ambientes, `.env` não é lido: configure o processo ou as chaves de configuração do servidor. `appsettings.Development.json` contém apenas exemplos públicos, sem credenciais.

BaseUrl inclui o caminho da versão `/v4/`; ImagesBaseUrl inclui `/igdb/image/upload/`. Bases são normalizadas para uma barra final, inclusive se a configuração tiver várias. O cliente acrescenta `games`; o mapper acrescenta `t_cover_big/{image_id}.jpg` usando composição de URI relativa. Prefixos de proxy são preservados. TokenEndpoint é o endereço completo, sem acrescentar segmentos.

Todas as URLs devem ser absolutas HTTPS, sem userinfo, query, fragmento ou barras invertidas. Valor vazio é configuração inválida; chave ausente utiliza o padrão. Configuração inválida impede a inicialização com mensagem genérica, sem ecoar valor ou guardar exceção interna. Nenhum endpoint recebe essas URLs via request.

## Código e comportamento

`IgdbAuthenticationRegistration` resolve opções imutáveis e injeta a mesma configuração em TwitchOAuthClient, IgdbAuthenticatedClient e IgdbGameMapper. Program exige sua resolução na inicialização. IgdbGameCatalog/ScopedExternalGameCatalog usam o mapper configurado tanto na busca quanto na descoberta por ID externo.

Somente os endereços são validados antecipadamente. Credenciais e aquisição de token continuam lazy, após necessidade de fornecedor. ClientId/ClientSecret permanecem exclusivos do backend, com segredo no formulário OAuth; IGDB recebe Client-ID e Bearer. Renovação após 401 mantém o mesmo destino configurado e o limite de uma repetição. Clientes nomeados continuam sem loggers HTTP, cookies, credenciais do sistema ou redirects automáticos; uma resposta 3xx não é seguida para outro host.

O mapper continua validando image_id e ignora URLs fornecidas no payload. Ausência de capa mantém null. Alterar ImagesBaseUrl afeta imagens mapeadas em novas consultas/importações; capas já persistidas no catálogo não são reescritas por esta PR. Cache, SQL, IDs locais integer e igdbId externo bigint preservam seus contratos.

## Validação e entrega

Commit RED `a4d37ba`: URLs customizadas eram ignoradas, valores inválidos não impediam inicialização e o leitor `.env` descartava as três chaves. Os testes usam valores sintéticos e handlers controlados, sem chamadas externas ou credenciais reais. Cobrem busca/descoberta, bases com zero/uma/várias barras, prefixos, imagens, headers/formulário, renovação após 401, validação segura, redirects e precedência do processo sobre arquivo local.

Suíte completa: 463 testes aprovados (435 API, 27 Domain, 1 Application), com 46 cenários novos. Um smoke IGDB live opt-in ignorado. Build Release com analisadores: zero avisos e zero erros. Logs locais de verificação ficam em TestResults ignorado pelo Git; sem chamadas externas nesta execução.

Branch `feat/sexta-07-urls-configuracao`, S-US-07. Nenhum arquivo Front, migration ou dado de desenvolvimento alterado. Sem remoto configurado: entrega local para revisão, sem publicação ou merge. O Front mantém seu ponto único VITE_API_BASE_URL apontando para a própria API; nenhuma chave IGDB/Twitch foi adicionada ao consumidor.
