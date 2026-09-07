using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuVisibleField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "visible",
                schema: "jsini",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "메뉴 노출 여부");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "visible",
                schema: "jsini",
                table: "menu");
        }
    }
}
