# S-PR-16 — API de conclusão do perfil

Entrega Back na branch `feat/sexta-16-completar-perfil-api`, base `da7fea1`. O formulário pertence à S-PR-17; nenhum arquivo Front foi alterado.

## Funcionalidade e contrato

GET e PUT `/api/v1/users/me/profile-completion` exigem Bearer e usam exclusivamente o dono da sessão. Ambos respondem 200 com `{ country, state, bio, avatarUrl, favoriteGameIds, favoriteGameCount, isComplete }`, sem cache público. A lista contém todos os jogos favoritos existentes do dono, com IDs locais int32, ordenados por ID.

PUT recebe, por exemplo, o corpo abaixo; substitua os IDs pelos jogos locais existentes obtidos no catálogo:

```json
{
  "country": "Brasil",
  "state": "São Paulo",
  "bio": "Gosto de jogos de estratégia.",
  "favoriteGameIds": [1, 2, 3],
  "avatarUrl": null
}
```

País/estado/bio são obrigatórios e não vazios após trim; limites após trim de 100/100/2000 caracteres. A seleção requer pelo menos três IDs positivos distintos existentes; aceita mais de três e normaliza duplicados. O conjunto enviado substitui o conjunto de jogos favoritos desejado, preservando os vínculos mantidos; reviews favoritas não são afetadas. Omitir avatarUrl preserva a imagem atual, null remove; URL informada segue a regra HTTP/HTTPS até 2048 caracteres. Avatar não participa da conclusão; não há upload de arquivos.

400 indica campos/seleção inválidos, inclusive userId extra; 401 ausência/invalidez da sessão, 404 jogo inexistente e 503 persistência indisponível. Falha não grava parte do formulário. Os erros seguem ApiError. País/estado não aparecem no usuário público, auth/me ou busca de pessoas.

PATCH `/api/v1/users/me/profile` também aceita country/state: omissão preserva, null/vazio limpa. Limpar localização/bio ou remover o terceiro jogo favorito torna a próxima consulta incompleta. A remoção comum continua permitida abaixo do mínimo de três. Não existe booleano de conclusão persistido.

## Código e persistência

Domain adiciona Country/State ao UserProfile e a política simples ProfileCompletionPolicy. Application expõe contratos tipados, valida o perfil e resolve jogos em lotes antes da transação. Infrastructure lê uma visão consistente em RepeatableRead e salva perfil/favoritos numa transação, revalidando a existência SQL dos jogos. As escritas de conclusão, PATCH de perfil e jogos favoritos usam o bloqueio da mesma linha de usuário para coordenar concorrência. A reconciliação aplica somente diferenças; repetir o mesmo PUT preserva IDs, datas de criação e timestamp de perfil.

A migration `20261016021914_ProfileCompletionLocation` adiciona apenas pais/estado varchar(100) nullable a perfis_usuarios. As contas antigas mantêm os dados e ficam sem localização. Nenhuma tabela, PK, FK, cascata ou coluna de conclusão nova. Down remove as duas colunas e perde os valores de localização; não é necessário no upgrade.

OpenAPI/Swagger documentam as duas operações, autorização, campos obrigatórios/opcionais, IDs e erros. O documento exportado inclui 42 operações funcionais e health.

## Verificação

Commit RED `6323401` antecede o código: ausência das operações e da política comprovada. Testes de domínio cobrem campos insuficientes e 0/2/3/4 jogos com duplicados. Integração HTTP/PostgreSQL isolada cobre privacidade, avatar, idempotência, reinício, recálculo pela edição comum/remoção, validação, rollback após SaveChanges SQL e concorrência com seleções diferentes. Reviews favoritas não entram no contador nem são alteradas. Upgrade da versão anterior preserva conta/perfil/favoritos e sessão. Auditoria reconstrói oito migrations e compara as 16 tabelas ao modelo EF, somente no banco de testes com guarda de nome/host/marcador.

Não há remoto configurado no Back; entrega e revisão locais, sem publicação ou merge. Configurações privadas e exclusão preexistente de .env.example preservadas.

Migration aplicada explicitamente no gamesdb local após validar o alvo, sem parâmetros secretos na CLI. Antes/depois: usuários 7/7, perfis 7/7, sessões 5/5, jogos favoritos 2/2, reviews favoritas 0/0 e migrations 7/8. Colunas país/estado conferidas nullable com limite 100. O catálogo passou de 105 a 106 jogos no intervalo observado; a migration contém somente ALTER TABLE de localização e não grava catálogo. Não houve reset, seed ou remoção no desenvolvimento nesta entrega. Reinicie a instância habitual da API para carregar os endpoints novos.

Suíte final: 513 testes aprovados (478 API, 34 Domain, 1 Application), somente smoke IGDB live opt-in ignorado. Build Release com analisadores: zero avisos e erros. Instância temporária respondeu health/Swagger/OpenAPI 200, exportou o contrato e foi encerrada.
