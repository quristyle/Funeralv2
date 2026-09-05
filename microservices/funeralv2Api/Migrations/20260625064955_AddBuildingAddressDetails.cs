using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingAddressDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_detail",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zip_code",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_detail",
                schema: "smfr",
                table: "buildings");

            migrationBuilder.DropColumn(
                name: "zip_code",
                schema: "smfr",
                table: "buildings");
        }
    }
}
