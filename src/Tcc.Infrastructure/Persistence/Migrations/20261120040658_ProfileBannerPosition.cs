using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfileBannerPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "banner_position_x",
                table: "perfis_usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "banner_position_y",
                table: "perfis_usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddCheckConstraint(
                name: "ck_perfis_banner_position_x",
                table: "perfis_usuarios",
                sql: "banner_position_x BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_perfis_banner_position_y",
                table: "perfis_usuarios",
                sql: "banner_position_y BETWEEN 0 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_perfis_banner_position_x",
                table: "perfis_usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "ck_perfis_banner_position_y",
                table: "perfis_usuarios");

            migrationBuilder.DropColumn(
                name: "banner_position_x",
                table: "perfis_usuarios");

            migrationBuilder.DropColumn(
                name: "banner_position_y",
                table: "perfis_usuarios");
        }
    }
}
