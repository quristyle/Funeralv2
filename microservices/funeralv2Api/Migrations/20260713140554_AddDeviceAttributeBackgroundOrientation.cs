using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAttributeBackgroundOrientation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "background_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                comment: "배경 이미지 방향 (HORIZONTAL, VERTICAL_LEFT, VERTICAL_RIGHT, INVERTED)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "background_orientation",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
