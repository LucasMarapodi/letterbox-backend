using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedReviewRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reviews_texto",
                table: "reviews");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_reviews_usuario_id_jogo_id",
                table: "reviews",
                columns: new[] { "usuario_id", "jogo_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviews_texto",
                table: "reviews",
                sql: "char_length(texto) <= 10000");

            // Import only rating-only pairs. Existing review IDs, text and relationships stay intact.
            // No application publication callbacks or retrospective notifications run during migration.
            migrationBuilder.Sql("""
                INSERT INTO reviews (usuario_id, jogo_id, texto, criado_em, atualizado_em)
                SELECT a.usuario_id, a.jogo_id, '', a.criado_em, a.atualizado_em
                FROM avaliacoes a
                WHERE NOT EXISTS (SELECT 1 FROM reviews r WHERE r.usuario_id=a.usuario_id AND r.jogo_id=a.jogo_id);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_avaliacoes_reviews_usuario_id_jogo_id",
                table: "avaliacoes",
                columns: new[] { "usuario_id", "jogo_id" },
                principalTable: "reviews",
                principalColumns: new[] { "usuario_id", "jogo_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_avaliacoes_reviews_usuario_id_jogo_id",
                table: "avaliacoes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_reviews_usuario_id_jogo_id",
                table: "reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviews_texto",
                table: "reviews");

            // Preserve reviews without text during downgrade; the old check applies to future writes.
            migrationBuilder.Sql("""
                ALTER TABLE reviews ADD CONSTRAINT ck_reviews_texto
                CHECK (char_length(texto) BETWEEN 1 AND 10000 AND texto ~ '[^[:space:]]') NOT VALID;
                """);
        }
    }
}
