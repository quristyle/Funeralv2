using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class build_add_photos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "building_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parking_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "building_photo_group_id",
                schema: "smfr",
                table: "buildings");

            migrationBuilder.DropColumn(
                name: "parking_photo_group_id",
                schema: "smfr",
                table: "buildings");
        }
    }
}
