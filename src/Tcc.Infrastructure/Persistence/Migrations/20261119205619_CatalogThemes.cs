using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "temas_obtidos_em",
                table: "jogos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "temas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temas", x => x.id);
                    table.CheckConstraint("ck_temas_igdb_positivo", "igdb_id > 0");
                    table.CheckConstraint("ck_temas_nome_obrigatorio", "length(btrim(nome)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "jogos_temas",
                columns: table => new
                {
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    tema_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogos_temas", x => new { x.jogo_id, x.tema_id });
                    table.ForeignKey(
                        name: "FK_jogos_temas_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jogos_temas_temas_tema_id",
                        column: x => x.tema_id,
                        principalTable: "temas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_jogos_temas_tema_id",
                table: "jogos_temas",
                column: "tema_id");

            migrationBuilder.CreateIndex(
                name: "IX_temas_igdb_id",
                table: "temas",
                column: "igdb_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jogos_temas");

            migrationBuilder.DropTable(
                name: "temas");

            migrationBuilder.DropColumn(
                name: "temas_obtidos_em",
                table: "jogos");
        }
    }
}
