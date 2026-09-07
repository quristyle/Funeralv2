using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSystemMenuFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "affix_tab",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "authority",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "badge",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "badge_type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "dom_cached",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "iframe_src",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "keep_alive",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "link",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "menu_visible_with_forbidden",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "affix_tab",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "authority",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "badge",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "badge_type",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "dom_cached",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "iframe_src",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "keep_alive",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "link",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "menu_visible_with_forbidden",
                schema: "scom",
                table: "system_menus");
        }
    }
}
