# Posição pública do banner de perfil

PATCH `/api/v1/users/me/profile` aceita bannerPositionX e bannerPositionY opcionais: inteiros JSON entre 0 e 100, sendo 50/50 o centro. Omitir ou enviar null preserva o eixo. Número fora da faixa, fracionário ou string é rejeitado. A URL não precisa ser reenviada ao alterar posição. Remoção explícita de bannerUrl restaura os dois eixos para 50.

GET público `/users/{id}`, login/me, resposta PATCH e GET profile-completion incluem ambos os eixos. PUT de conclusão continua preservando a posição ao alterar seus demais campos. Não há controle de enquadramento de banners de jogos nesta mudança.

Migration `20261120040658_ProfileBannerPosition`: acrescenta duas colunas integer NOT NULL com default50 em perfis_usuarios e CHECKs de faixa. Perfis existentes permanecem centralizados. Não altera URLs, IDs, favoritos ou sessão. Default EF usa sentinel -1 para preservar 0 como posição válida. Ownership/FK RESTRICT foi preservado explicitamente no snapshot e Designer, conforme a convenção existente.

TDD: novo teste HTTP reproduziu400 antes do contrato, commit ed5d784. 41 testes direcionados aprovados: novos contratos, identidade/perfil, modelo/snapshot, banners e edição parcial SQL. Persistência após novo host, valor0, omissão e remoção cobertos no banco exclusivo tcc_pr01_tests. Um cenário Chrome desktop integrado do Front confirmou posição SQL, leitura pública após reload e formulário real. Sem suíte completa/mobile ou fonte externa IGDB.

Em 20/11/2026, após autorização para disponibilizar o recurso local, a migration foi aplicada também ao gamesdb; histórico confirmado, API recompilada e reiniciada. Front, health, perfil público e destaque retornaram200. OpenAPI exportado da API atual. Sem seed ou alteração de contas reais nesta entrega.
