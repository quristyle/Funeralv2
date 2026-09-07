using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSortOrderToAppRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sortorder",
                schema: "helpdesk",
                table: "approle",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "정렬 순서");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sortorder",
                schema: "helpdesk",
                table: "approle");
        }
    }
}
