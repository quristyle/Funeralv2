using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class device_add_col : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "portrait_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "video_orientation",
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
                name: "portrait_orientation",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "video_orientation",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
