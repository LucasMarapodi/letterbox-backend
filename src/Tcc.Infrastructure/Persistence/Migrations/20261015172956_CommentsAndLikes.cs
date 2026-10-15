using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommentsAndLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comentarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    review_id = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    comentario_pai_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comentarios", x => x.id);
                    table.UniqueConstraint("AK_comentarios_review_id_id", x => new { x.review_id, x.id });
                    table.CheckConstraint("ck_comentarios_sem_autopai", "comentario_pai_id IS NULL OR comentario_pai_id <> id");
                    table.CheckConstraint("ck_comentarios_texto", "char_length(texto) BETWEEN 1 AND 2000 AND texto ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_comentarios_comentarios_review_id_comentario_pai_id",
                        columns: x => new { x.review_id, x.comentario_pai_id },
                        principalTable: "comentarios",
                        principalColumns: new[] { "review_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_comentarios_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_comentarios_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "curtidas_comentarios",
                columns: table => new
                {
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    comentario_id = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curtidas_comentarios", x => new { x.usuario_id, x.comentario_id });
                    table.ForeignKey(
                        name: "FK_curtidas_comentarios_comentarios_comentario_id",
                        column: x => x.comentario_id,
                        principalTable: "comentarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_curtidas_comentarios_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_criado_em_review_id",
                table: "comentarios",
                columns: new[] { "criado_em", "review_id" });

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_review_id_comentario_pai_id",
                table: "comentarios",
                columns: new[] { "review_id", "comentario_pai_id" });

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_review_id_criado_em_id",
                table: "comentarios",
                columns: new[] { "review_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_comentarios_usuario_id",
                table: "comentarios",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_curtidas_comentarios_comentario_id",
                table: "curtidas_comentarios",
                column: "comentario_id");

            // Published comments cannot change review or parent. This also prevents
            // concurrent reparenting from turning a valid root into a second-level reply.
            migrationBuilder.Sql("""
                CREATE FUNCTION validar_hierarquia_comentario() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE pai comentarios%ROWTYPE;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF ROW(NEW.id, NEW.review_id, NEW.comentario_pai_id)
                            IS DISTINCT FROM ROW(OLD.id, OLD.review_id, OLD.comentario_pai_id) THEN
                            RAISE EXCEPTION 'Vinculo de comentario publicado e imutavel.' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    IF NEW.comentario_pai_id IS NOT NULL THEN
                        IF NEW.comentario_pai_id = NEW.id THEN
                            RAISE EXCEPTION 'Comentario nao pode responder a si.' USING ERRCODE = '23514';
                        END IF;
                        SELECT * INTO pai FROM comentarios WHERE id = NEW.comentario_pai_id;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'Comentario pai inexistente.' USING ERRCODE = '23503';
                        END IF;
                        IF pai.comentario_pai_id IS NOT NULL THEN
                            RAISE EXCEPTION 'Somente uma camada de respostas.' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER trg_comentarios_hierarquia
                BEFORE INSERT OR UPDATE OF id, review_id, comentario_pai_id ON comentarios
                FOR EACH ROW EXECUTE FUNCTION validar_hierarquia_comentario();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER trg_comentarios_hierarquia ON comentarios; DROP FUNCTION validar_hierarquia_comentario();");
            migrationBuilder.DropTable(
                name: "curtidas_comentarios");

            migrationBuilder.DropTable(
                name: "comentarios");
        }
    }
}
