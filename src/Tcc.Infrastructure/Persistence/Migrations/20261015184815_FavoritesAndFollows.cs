using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FavoritesAndFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jogos_favoritos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogos_favoritos", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogos_favoritos_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jogos_favoritos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reviews_favoritas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    review_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews_favoritas", x => x.id);
                    table.ForeignKey(
                        name: "FK_reviews_favoritas_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reviews_favoritas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimentos_usuarios",
                columns: table => new
                {
                    seguidor_id = table.Column<int>(type: "integer", nullable: false),
                    seguido_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seguimentos_usuarios", x => new { x.seguidor_id, x.seguido_id });
                    table.CheckConstraint("ck_seguimentos_sem_autoseguimento", "seguidor_id <> seguido_id");
                    table.ForeignKey(
                        name: "FK_seguimentos_usuarios_usuarios_seguido_id",
                        column: x => x.seguido_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seguimentos_usuarios_usuarios_seguidor_id",
                        column: x => x.seguidor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_jogos_favoritos_jogo_id",
                table: "jogos_favoritos",
                column: "jogo_id");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_favoritos_usuario_id_jogo_id",
                table: "jogos_favoritos",
                columns: new[] { "usuario_id", "jogo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_favoritas_review_id",
                table: "reviews_favoritas",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_favoritas_usuario_id_review_id",
                table: "reviews_favoritas",
                columns: new[] { "usuario_id", "review_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seguimentos_usuarios_seguido_id_seguidor_id",
                table: "seguimentos_usuarios",
                columns: new[] { "seguido_id", "seguidor_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jogos_favoritos");

            migrationBuilder.DropTable(
                name: "reviews_favoritas");

            migrationBuilder.DropTable(
                name: "seguimentos_usuarios");
        }
    }
}
