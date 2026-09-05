using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConversionLogsToMediaSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "conversioncommand",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "conversioncompletedat",
                schema: "smfr",
                table: "media_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "conversionstartedat",
                schema: "smfr",
                table: "media_sources",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "conversioncommand",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "conversioncompletedat",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "conversionstartedat",
                schema: "smfr",
                table: "media_sources");
        }
    }
}
