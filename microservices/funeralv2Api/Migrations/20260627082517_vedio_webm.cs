using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class vedio_webm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hasthumbnail",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "haswebm",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hasthumbnail",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "haswebm",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "smfr",
                table: "media_sources");
        }
    }
}
