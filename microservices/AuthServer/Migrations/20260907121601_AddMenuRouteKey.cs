using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuRouteKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "route_key",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "이 메뉴가 가리키는 화면의 열쇠 (예: funeral.room-status)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "route_key",
                schema: "scom",
                table: "system_menus");
        }
    }
}
