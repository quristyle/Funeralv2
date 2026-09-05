using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCodeAndAddShortNameToBuilding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "code",
                schema: "smfr",
                table: "buildings");

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "short_name",
                schema: "smfr",
                table: "buildings");

            migrationBuilder.AddColumn<string>(
                name: "code",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
