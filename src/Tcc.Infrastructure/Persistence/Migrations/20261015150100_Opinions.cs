using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Opinions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "avaliacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    nota = table.Column<double>(type: "double precision", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avaliacoes", x => x.id);
                    table.CheckConstraint("ck_avaliacoes_nota", "nota >= 0 AND nota <= 5 AND nota <> 'NaN'::double precision");
                    table.ForeignKey(
                        name: "FK_avaliacoes_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_avaliacoes_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_texto", "char_length(texto) BETWEEN 1 AND 10000 AND texto ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_reviews_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reviews_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_jogo_id",
                table: "avaliacoes",
                column: "jogo_id");

            migrationBuilder.CreateIndex(
                name: "IX_avaliacoes_usuario_id_jogo_id",
                table: "avaliacoes",
                columns: new[] { "usuario_id", "jogo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_criado_em_id",
                table: "reviews",
                columns: new[] { "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_jogo_id_criado_em_id",
                table: "reviews",
                columns: new[] { "jogo_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_usuario_id_criado_em_id",
                table: "reviews",
                columns: new[] { "usuario_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_usuario_id_jogo_id",
                table: "reviews",
                columns: new[] { "usuario_id", "jogo_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avaliacoes");

            migrationBuilder.DropTable(
                name: "reviews");
        }
    }
}
