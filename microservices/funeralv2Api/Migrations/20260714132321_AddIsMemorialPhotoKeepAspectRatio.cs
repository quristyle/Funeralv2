using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMemorialPhotoKeepAspectRatio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_memorial_photo_keep_aspect_ratio",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "영정사진 비율 유지 여부");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_memorial_photo_keep_aspect_ratio",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
