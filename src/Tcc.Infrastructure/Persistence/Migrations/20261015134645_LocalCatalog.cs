using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generos", x => x.id);
                    table.CheckConstraint("ck_generos_igdb_positivo", "igdb_id > 0");
                });

            migrationBuilder.CreateTable(
                name: "jogos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    capa_url = table.Column<string>(type: "text", nullable: true),
                    primeira_data_lancamento = table.Column<DateOnly>(type: "date", nullable: true),
                    igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    dados_obtidos_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogos", x => x.id);
                    table.CheckConstraint("ck_jogos_igdb_positivo", "igdb_id > 0");
                });

            migrationBuilder.CreateTable(
                name: "plataformas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plataformas", x => x.id);
                    table.CheckConstraint("ck_plataformas_igdb_positivo", "igdb_id > 0");
                });

            migrationBuilder.CreateTable(
                name: "jogos_generos",
                columns: table => new
                {
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    genero_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogos_generos", x => new { x.jogo_id, x.genero_id });
                    table.ForeignKey(
                        name: "FK_jogos_generos_generos_genero_id",
                        column: x => x.genero_id,
                        principalTable: "generos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jogos_generos_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jogos_plataformas",
                columns: table => new
                {
                    jogo_id = table.Column<int>(type: "integer", nullable: false),
                    plataforma_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogos_plataformas", x => new { x.jogo_id, x.plataforma_id });
                    table.ForeignKey(
                        name: "FK_jogos_plataformas_jogos_jogo_id",
                        column: x => x.jogo_id,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jogos_plataformas_plataformas_plataforma_id",
                        column: x => x.plataforma_id,
                        principalTable: "plataformas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generos_igdb_id",
                table: "generos",
                column: "igdb_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jogos_igdb_id",
                table: "jogos",
                column: "igdb_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jogos_generos_genero_id",
                table: "jogos_generos",
                column: "genero_id");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_plataformas_plataforma_id",
                table: "jogos_plataformas",
                column: "plataforma_id");

            migrationBuilder.CreateIndex(
                name: "IX_plataformas_igdb_id",
                table: "plataformas",
                column: "igdb_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jogos_generos");

            migrationBuilder.DropTable(
                name: "jogos_plataformas");

            migrationBuilder.DropTable(
                name: "generos");

            migrationBuilder.DropTable(
                name: "jogos");

            migrationBuilder.DropTable(
                name: "plataformas");
        }
    }
}
