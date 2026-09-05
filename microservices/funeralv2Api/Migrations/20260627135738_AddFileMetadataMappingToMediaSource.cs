using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFileMetadataMappingToMediaSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "aacfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "oggfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "originalfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "thumbnailfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "webmfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aacfileid",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "oggfileid",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "originalfileid",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "thumbnailfileid",
                schema: "smfr",
                table: "media_sources");

            migrationBuilder.DropColumn(
                name: "webmfileid",
                schema: "smfr",
                table: "media_sources");
        }
    }
}
