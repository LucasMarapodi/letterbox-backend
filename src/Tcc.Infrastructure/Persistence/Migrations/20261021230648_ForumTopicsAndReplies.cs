using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForumTopicsAndReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "topicos_forum",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    texto = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topicos_forum", x => x.id);
                    table.CheckConstraint("ck_topicos_forum_texto", "char_length(texto) BETWEEN 1 AND 10000 AND texto ~ '[^[:space:]]'");
                    table.CheckConstraint("ck_topicos_forum_titulo", "char_length(titulo) BETWEEN 1 AND 150 AND titulo ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_topicos_forum_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_topicos_forum_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "respostas_forum",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    topico_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_respostas_forum", x => x.id);
                    table.CheckConstraint("ck_respostas_forum_texto", "char_length(texto) BETWEEN 1 AND 10000 AND texto ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_respostas_forum_topicos_forum_topico_id",
                        column: x => x.topico_id,
                        principalTable: "topicos_forum",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_respostas_forum_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_respostas_forum_topico_id_criado_em_id",
                table: "respostas_forum",
                columns: new[] { "topico_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_respostas_forum_usuario_id",
                table: "respostas_forum",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_topicos_forum_jogo_id_criado_em_id",
                table: "topicos_forum",
                columns: new[] { "jogo_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_topicos_forum_usuario_id",
                table: "topicos_forum",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "respostas_forum");

            migrationBuilder.DropTable(
                name: "topicos_forum");
        }
    }
}
