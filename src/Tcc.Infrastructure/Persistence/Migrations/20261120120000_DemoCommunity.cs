using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations;

/// <summary>Contas e conteúdo demonstrativos; senha de acesso: TccDemo@2026!.</summary>
public partial class DemoCommunity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TEMP TABLE demo_pessoas (
                n integer PRIMARY KEY, nome text NOT NULL, email text NOT NULL,
                estado text NOT NULL, bio text NOT NULL, avatar text NOT NULL,
                senha_hash text NOT NULL,
                usuario_id integer UNIQUE, review_id integer UNIQUE
            ) ON COMMIT DROP;

            INSERT INTO demo_pessoas(n,nome,email,estado,bio,avatar,senha_hash) VALUES
            (1,'Mariana Costa','mariana.costa@mock.tcc.example','São Paulo',
             'Designer de interfaces e suporte no LoL. Gosto de partidas em equipe, jogos independentes e de conversar sobre acessibilidade nos games.',
             'https://ui-avatars.com/api/?name=Mariana+Costa&background=7c3aed&color=ffffff', 'pbkdf2-sha512:210000:KLLSJUKabg7v2Yuj+ifdwQ==:tzDtFXrq7yndKhbiFSf9HeJcJW2oBpIF7PSaIRxYqiI='),
            (2,'Rafael Almeida','rafael.almeida@mock.tcc.example','Rio de Janeiro',
             'Estudante de engenharia e fã de Fortnite sem construção. Jogo com amigos depois da aula e gosto de descobrir estratégias para o squad.',
             'https://ui-avatars.com/api/?name=Rafael+Almeida&background=2563eb&color=ffffff', 'pbkdf2-sha512:210000:ZhZOZGJZW7T8wdGbPIm4uw==:PGEqAcvUvlmMuZvpX/VEUpBIG25LsAuQgjAI5fLkoss='),
            (3,'Beatriz Santos','beatriz.santos@mock.tcc.example','Minas Gerais',
             'Desenvolvedora e jogadora de meio no LoL. Curto aprender novos campeões, acompanhar o competitivo e compartilhar dicas sem pressão por elo.',
             'https://ui-avatars.com/api/?name=Beatriz+Santos&background=be185d&color=ffffff', 'pbkdf2-sha512:210000:sFzD5uKWQIUKAipMl/WPTw==:JS3GUpOyYdMqtgJJMiX6jG/KkX8jn4PtgdKKTmqEQ7I='),
            (4,'Gabriel Oliveira','gabriel.oliveira@mock.tcc.example','São Paulo',
             'Analista de dados, jogador casual e explorador dos mapas do Fortnite. Prefiro comunicação tranquila e partidas que rendem boas histórias.',
             'https://ui-avatars.com/api/?name=Gabriel+Oliveira&background=047857&color=ffffff', 'pbkdf2-sha512:210000:1huJCIFvrDmO7XZyoaJa9A==:QKjmNgFKWSlghdemw2RBIzWq68UeleWYysv5drPmXhI='),
            (5,'Camila Rocha','camila.rocha@mock.tcc.example','Rio de Janeiro',
             'Professora de inglês e fã do universo de Runeterra. Jogo de atiradora com amigos e adoro trilhas sonoras e histórias dos personagens.',
             'https://ui-avatars.com/api/?name=Camila+Rocha&background=c2410c&color=ffffff', 'pbkdf2-sha512:210000:y2qGST/T5AoQoLdckA4g3g==:kfYFcLp966EZBWedwxaYy6ALohhalKZtwEuL3FCiM4w='),
            (6,'Lucas Ferreira','lucas.ferreira@mock.tcc.example','Minas Gerais',
             'Estudante de sistemas de informação. Alterno entre Fortnite e jogos de aventura; gosto de testar mecânicas e ajudar quem está começando.',
             'https://ui-avatars.com/api/?name=Lucas+Ferreira&background=4338ca&color=ffffff', 'pbkdf2-sha512:210000:fZkCh2zArvUf+9zNX8vtUg==:J+Ff60kN4saXN+6VAvIRiAoI0ihd6McxV474CFBpj9Q=');

            DELETE FROM demo_pessoas WHERE email IN (SELECT email FROM usuarios);

            -- Preserva contas existentes; username é calculado pelo banco.
            INSERT INTO usuarios(email,senha_hash,criado_em,atualizado_em)
            SELECT email,senha_hash,CURRENT_TIMESTAMP - interval '7 days',CURRENT_TIMESTAMP
            FROM demo_pessoas ORDER BY n ON CONFLICT (email) DO NOTHING;
            UPDATE demo_pessoas p SET usuario_id=u.id FROM usuarios u WHERE u.email=p.email;

            -- 2. Perfis: todos com nome, bio, avatar e localização compatível com a aplicação.
            INSERT INTO perfis_usuarios(usuario_id,nome_exibicao,biografia,avatar_url,pais,estado,atualizado_em)
            SELECT usuario_id,nome,bio,avatar,'Brasil',estado,CURRENT_TIMESTAMP FROM demo_pessoas;

            INSERT INTO jogos(igdb_id,nome,descricao,capa_url,primeira_data_lancamento,dados_obtidos_em,criado_em,atualizado_em) VALUES
             (115,'League of Legends','Jogo competitivo de estratégia em equipe, com campeões de habilidades distintas e disputas pelo controle do mapa.',
              'https://static-cdn.jtvnw.net/ttv-boxart/21779-285x380.jpg',DATE '2009-10-27',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
             (1905,'Fortnite','Experiência multijogador com combates, exploração e modos para jogar em grupo, incluindo partidas de battle royale.',
              'https://static-cdn.jtvnw.net/ttv-boxart/33214-285x380.jpg',NULL,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP),
             (127354,'Convergence: A League of Legends Story','Aventura de ação e plataforma ambientada no universo de League of Legends.',
              NULL,NULL,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)
            ON CONFLICT (igdb_id) DO NOTHING;

            CREATE TEMP TABLE demo_jogos ON COMMIT DROP AS
            SELECT id,igdb_id,nome FROM jogos WHERE igdb_id IN (115,1905,127354);

            CREATE TEMP TABLE demo_textos (
             n integer PRIMARY KEY,igdb_id bigint,titulo text,texto text,nota double precision
            ) ON COMMIT DROP;
            INSERT INTO demo_textos VALUES
             (1,115,'Estratégia em equipe que sempre ensina algo novo',
             'O que mais gosto no LoL é perceber como visão de mapa e comunicação mudam uma partida. Jogar de suporte com amigos torna cada objetivo uma decisão em conjunto. A quantidade de campeões dá vontade de aprender sempre, mas a experiência para iniciantes poderia explicar melhor alguns sistemas. Recomendo começar em grupo e manter o chat tranquilo.',4.5),
             (2,1905,'Sem construção virou meu programa com os amigos',
             'Fortnite funciona muito bem para reunir o pessoal depois da aula. No modo sem construção consigo me concentrar em posicionamento, movimentação e comunicação. Gosto de como cada partida cria situações diferentes. Alguns menus poderiam ser mais diretos e nem toda mudança me agrada, mas ainda é um dos jogos em que mais me divirto em squad.',4.5),
             (3,115,'Aprender os confrontos faz toda a diferença',
             'Depois de focar em poucos campeões no meio, comecei a aproveitar muito mais o jogo. A profundidade das decisões e as possibilidades de jogar em equipe são excelentes. Ao mesmo tempo, exige paciência e o ambiente competitivo pode cansar. Para mim, vale muito a pena quando o objetivo é melhorar com calma e jogar com pessoas que respeitam o grupo.',4.0),
             (4,1905,'Melhor quando o squad conversa',
             'Minha melhor experiência no Fortnite vem das partidas em que combinamos a queda e compartilhamos recursos. A movimentação e a variedade de situações deixam as sessões leves. Às vezes sinto uma diferença grande de habilidade entre os adversários, então prefiro encarar como diversão casual. O modo sem construção é minha recomendação para quem está voltando.',4.0),
             (5,115,'Runeterra me trouxe, a bot lane me fez ficar',
             'Comecei pelo interesse nos personagens e fiquei pela dinâmica de jogar em dupla. Quando atirador e suporte se entendem, até uma partida difícil rende bons momentos. Adoro a identidade visual e as músicas, mas acho a curva de aprendizado alta e gostaria de uma introdução mais acolhedora. Com amigos e expectativas realistas, continuo me divertindo bastante.',4.5),
             (6,1905,'Um jogo para experimentar sem levar tudo tão a sério',
             'Gosto de alternar partidas mais competitivas com sessões em que o objetivo é explorar e testar possibilidades com os amigos. Fortnite entrega essa liberdade e costuma render boas histórias. A quantidade de opções pode confundir no começo, mas depois que encontrei meus modos favoritos ficou fácil voltar. Jogo principalmente no PC e valorizo mais a cooperação do que a vitória.',4.0);

            INSERT INTO reviews(usuario_id,jogo_id,titulo,texto,criado_em,atualizado_em)
            SELECT p.usuario_id,j.id,t.titulo,t.texto,
                   CURRENT_TIMESTAMP - interval '3 hours' + t.n * interval '5 minutes',NULL
            FROM demo_textos t JOIN demo_pessoas p ON p.n=t.n JOIN demo_jogos j ON j.igdb_id=t.igdb_id
            ORDER BY t.n;
            UPDATE demo_pessoas p SET review_id=r.id FROM reviews r WHERE r.usuario_id=p.usuario_id;
            INSERT INTO avaliacoes(usuario_id,jogo_id,nota,criado_em,atualizado_em)
            SELECT p.usuario_id,j.id,t.nota,r.criado_em,r.criado_em
            FROM demo_textos t JOIN demo_pessoas p ON p.n=t.n
            JOIN demo_jogos j ON j.igdb_id=t.igdb_id JOIN reviews r ON r.id=p.review_id;

            INSERT INTO comentarios(usuario_id,review_id,comentario_pai_id,texto,criado_em)
            SELECT outro.usuario_id,autor.review_id,NULL,
                   CASE WHEN autor.n % 2 = 1
                        THEN 'Concordo! Jogar em equipe e conversar faz muita diferença no LoL.'
                        ELSE 'Também prefiro o modo sem construção. Vamos combinar um squad!'
                   END,CURRENT_TIMESTAMP - interval '30 minutes'
            FROM demo_pessoas autor JOIN demo_pessoas outro ON outro.n=(autor.n % 6)+1;
            CREATE TEMP TABLE demo_topicos(igdb_id bigint PRIMARY KEY,n integer,titulo text,texto text,topico_id integer) ON COMMIT DROP;
            INSERT INTO demo_topicos(igdb_id,n,titulo,texto) VALUES
             (115,1,'Comunidade LoL: duo, dicas e partidas sem pressão',
              'Espaço para reunir quem quer jogar League of Legends com respeito e vontade de aprender. Contem sua rota favorita, compartilhem uma dica e combinem partidas em grupo. Eu jogo de suporte e costumo entrar à noite.'),
             (1905,2,'Comunidade Fortnite: squad sem construção',
              'Vamos montar um grupo para partidas casuais de Fortnite sem construção? A ideia é conversar, dividir recursos e receber quem está começando. Digam como gostam de jogar e quais estratégias funcionam para o squad de vocês.');
            INSERT INTO topicos_forum(jogo_id,usuario_id,titulo,texto,criado_em)
            SELECT j.id,p.usuario_id,t.titulo,t.texto,CURRENT_TIMESTAMP - interval '4 hours'
            FROM demo_topicos t JOIN demo_pessoas p ON p.n=t.n JOIN demo_jogos j ON j.igdb_id=t.igdb_id;
            UPDATE demo_topicos t SET topico_id=f.id FROM topicos_forum f JOIN demo_pessoas p ON p.usuario_id=f.usuario_id
            WHERE p.n=t.n AND f.titulo=t.titulo;
            INSERT INTO respostas_forum(topico_id,usuario_id,texto,criado_em)
            SELECT t.topico_id,p.usuario_id,
             CASE p.n
             WHEN 1 THEN 'Posso jogar com vocês depois do trabalho. Gosto de combinar a estratégia antes da partida e ajudar quem está aprendendo.'
             WHEN 2 THEN 'Topo participar! No Fortnite prefiro sem construção e um ponto de queda tranquilo para organizar o grupo.'
             WHEN 3 THEN 'No LoL eu jogo no meio. Acho legal combinar um objetivo por partida para todo mundo aprender junto.'
             WHEN 4 THEN 'Contem comigo para o squad. Costumo avisar sobre recursos e prefiro manter o time perto nas rotações.'
             WHEN 5 THEN 'Adorei a proposta. Jogo de atiradora no LoL e gosto de grupos que conversam sem pressionar ninguém.'
             WHEN 6 THEN 'Também quero participar. Posso ajudar os iniciantes com os controles e com a exploração do mapa.' END,
             CURRENT_TIMESTAMP - interval '15 minutes' + p.n * interval '1 minute'
            FROM demo_topicos t JOIN demo_pessoas p ON (p.n % 2) = (t.n % 2) WHERE t.topico_id IS NOT NULL;


            DROP TABLE demo_topicos, demo_textos, demo_jogos, demo_pessoas;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Carga aditiva: preserva contas e conteúdo que podem ter recebido interações reais.
        // O rollback retira a versão da migration, sem apagar dados de usuários ou catálogo.
    }
}