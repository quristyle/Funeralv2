using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class room_shotname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "short_name",
                schema: "smfr",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "short_name",
                schema: "smfr",
                table: "devices");
        }
    }
}
