using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_horizontal_alignment",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "photo_vertical_alignment",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_horizontal_alignment",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "photo_vertical_alignment",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
