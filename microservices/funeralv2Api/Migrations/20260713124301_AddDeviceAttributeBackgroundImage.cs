using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAttributeBackgroundImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "background_image_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "재생 배경 이미지 식별자 (ID)");

            migrationBuilder.AddColumn<bool>(
                name: "is_background_image_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "배경 이미지 사용 여부");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "background_image_id",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "is_background_image_enabled",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
