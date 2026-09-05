using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOggAacToMediaSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aacurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "hasaac",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "hasogg",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "oggurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aacurl",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "hasaac",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "hasogg",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "oggurl",
                schema: "smfr",
                table: "media_sources");
        }
    }
}
