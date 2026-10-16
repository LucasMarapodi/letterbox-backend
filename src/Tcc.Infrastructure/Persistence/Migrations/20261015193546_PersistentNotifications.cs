using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    destinatario_id = table.Column<int>(type: "integer", nullable: false),
                    ator_id = table.Column<int>(type: "integer", nullable: false),
                    review_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comentario_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificacoes", x => x.id);
                    table.CheckConstraint("ck_notificacoes_sem_auto", "destinatario_id <> ator_id");
                    table.CheckConstraint("ck_notificacoes_tipo_comentario", "(tipo = 'new-review' AND comentario_id IS NULL) OR (tipo = 'review-comment' AND comentario_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_notificacoes_comentarios_review_id_comentario_id",
                        columns: x => new { x.review_id, x.comentario_id },
                        principalTable: "comentarios",
                        principalColumns: new[] { "review_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notificacoes_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notificacoes_usuarios_ator_id",
                        column: x => x.ator_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notificacoes_usuarios_destinatario_id",
                        column: x => x.destinatario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_ator_id",
                table: "notificacoes",
                column: "ator_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_destinatario_id_comentario_id",
                table: "notificacoes",
                columns: new[] { "destinatario_id", "comentario_id" },
                unique: true,
                filter: "tipo = 'review-comment'");

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_destinatario_id_criado_em_id",
                table: "notificacoes",
                columns: new[] { "destinatario_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_destinatario_id_review_id",
                table: "notificacoes",
                columns: new[] { "destinatario_id", "review_id" },
                unique: true,
                filter: "tipo = 'new-review'");

            migrationBuilder.CreateIndex(
                name: "IX_notificacoes_review_id_comentario_id",
                table: "notificacoes",
                columns: new[] { "review_id", "comentario_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacoes");
        }
    }
}
