# S-PR-32 — Edição parcial unificada do perfil na API

Branch `feat/sexta-32-edicao-perfil-parcial`, base `5977388`. RED `3271fc1` confirmado e commitado antes do código. Entrega exclusivamente Back; o modal pertence à S-PR-33. Evidências históricas da S-PR-16 permanecem preservadas.

## Contrato

PATCH `/api/v1/users/me/profile` passa a salvar nome, país, estado, bio, avatar e conjunto de jogos favoritos numa transação. Perfil incompleto pode ser salvo, inclusive somente nome/avatar, localização parcial e zero/um/dois favoritos. A conclusão é calculada separadamente: país/estado/bio preenchidos e pelo menos três jogos distintos existentes, sem exigir avatar.

Exemplo de salvamento incompleto (IDs devem existir no catálogo local):

```json
{
  "displayName": "Meu nome",
  "country": "Brasil",
  "state": null,
  "bio": null,
  "favoriteGameIds": [1, 2]
}
```

Todos os campos são opcionais no PATCH. Omitir preserva; nome informado exige texto não vazio após trim, até 100 caracteres, e não aceita null. Bio null/vazio/espaços limpa, até 2000 caracteres após trim. Avatar omitido preserva; null/vazio/espaços limpa; URL informada segue HTTP/HTTPS até 2048 caracteres. País/estado null/vazio limpa cada campo, sujeitos à validação do par final.

favoriteGameIds omitido preserva todos os favoritos; null é inválido; [] limpa; IDs devem ser números JSON inteiros positivos existentes. Repetições são deduplicadas; não há mínimo nem truncamento para três/cinco. O conjunto representa somente jogos favoritos: reviews favoritas permanecem independentes. Repetir um PATCH igual preserva vínculos, datas dos favoritos mantidos e timestamp de perfil quando os valores não mudam.

A resposta privada 200 é ProfileUpdateResponse, com os campos anteriores no nível raiz mais completion:

```json
{
  "id": 1,
  "username": "user-1",
  "displayName": "Meu nome",
  "bio": "",
  "avatarUrl": null,
  "completion": {
    "country": "Brasil",
    "state": null,
    "bio": "",
    "avatarUrl": null,
    "favoriteGameIds": [1, 2],
    "favoriteGameCount": 2,
    "isComplete": false
  }
}
```

Essa extensão preserva consumidores anteriores que decodificam os campos do usuário na raiz; o novo consumidor lê também completion, sem segunda escrita ou consulta para descobrir o resultado salvo. PublicUserResponse não muda e não recebe localização/completion, inclusive em perfil público e auth/me. PATCH e leitura de conclusão usam no-store. GET `/users/me/profile-completion` mantém o contrato privado existente.

## Localização e compatibilidade

ProfileLocations centraliza os labels exatos, normalizados por trim: Brasil → São Paulo, Rio de Janeiro, Minas Gerais; Estados Unidos → Califórnia, Nova York; China → Taiwan, Hubei. Não são IDs novos nem dados obtidos por serviço geográfico. País selecionado com estado vazio é válido para salvar; estado preenchido sem país ou de outro país é inválido.

A validação compõe primeiro os valores finais, mesmo se somente country ou state for enviado. Por isso, para limpar um país que possui estado, envie ambos como null/vazio; omitir estado o preserva e torna esse par inválido. Trocar Brasil/São Paulo por China exige enviar também um estado compatível ou vazio.

Localização legada fora do catálogo é preservada exatamente, inclusive espaços históricos, quando omitida ou reenviada sem mudança. Alterar de fato um componente exige que o par final use as opções atuais. Não há conversão/exclusão automática desses valores.

PUT `/users/me/profile-completion` permanece para compatibilidade, documentado como legado no OpenAPI. Delega ao mesmo caso de uso/store, também aceita incompletos e preserva o nome. Seu conjunto favoriteGameIds continua obrigatório; localização/bio omitidos/null são limpos por semântica de substituição; avatar omitido preserva. Payloads antigos válidos continuam aceitos. O modal novo deve usar somente PATCH.

## Código e validação

UserProfile.Apply compartilha a normalização e validação do par alterado. ProfileCompletion.Patch valida o comando e resolve IDs em lotes antes da transação; EditUserProfile e o PUT legado delegam a esse fluxo. PostgresProfileCompletion reutiliza o lock da conta, carrega os valores atuais após o lock, revalida o par/jogos e reconcilia somente diferenças dos favoritos. A comparação inclui displayName. Campos omitidos usam o estado atual sob lock, sem sobrescrever uma alteração concorrente. Nome, localização, bio, avatar e favoritos são confirmados ou revertidos juntos.

Erros: 400 campos/par/IDs inválidos ou favoritos null; 401 sessão ausente/inválida; 404 jogo inexistente; 503 persistência indisponível. Não aceita userId/identidade no corpo. Nenhum booleano de conclusão persistido, store de negócio em memória, pacote, tabela ou migration novo. O schema permanece com 17 tabelas/dez migrations, sem alterações pendentes no modelo EF.

A busca para o modal permanece a agregadora existente: GET `/api/v1/search?mode=suggestions&scope=games&q=...`, até cinco sugestões com nome/capa e IDs locais; esse limite não restringe a quantidade de favoritos. Nenhuma alteração no catálogo ou no Front nesta entrega.

## Evidências e entrega

549 testes aprovados (496 API, 52 Domain, 1 Application), apenas smoke IGDB live opt-in ignorado. Build Release/analisadores com zero avisos e erros. Testes reais no PostgreSQL exclusivo, guardado por host/nome/marcador, cobrem 0/2/3/6 favoritos, duplicados, omissão/limpeza, nome/avatar, idempotência, sete pares de localização e valores vazios, legado com espaços, falha após gravação SQL com rollback integral e edição concorrente com campos omitidos. Regressões existentes continuam verificando autorização, privacidade, reinício e reviews favoritas independentes.

OpenAPI documenta ProfileUpdateResponse e campos PATCH opcionais, sem mínimo de favoritos para salvar; PublicUserResponse permanece intacto. Health/Swagger/OpenAPI 200 em instância temporária encerrada após exportação, com 45 operações funcionais e health. A porta 5089 já estava ocupada e foi preservada; a verificação usou 5098. Sem migration, alteração de banco de desenvolvimento, pacote ou configuração privada nesta entrega. A suíte continua auditando dez migrations/17 tabelas e ausência de alterações pendentes no modelo EF.

Reinicie a API habitual para carregar o novo contrato. Sem remoto configurado no Back: entrega/revisão e commits locais, sem publicação ou merge. Nenhum arquivo Front modificado; contrato disponível para S-PR-33.
