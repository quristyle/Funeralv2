using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class device_add_colss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "display_padding_bottom",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "display_padding_left",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "display_padding_right",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "display_padding_top",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "memorial_padding_bottom",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "memorial_padding_left",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "memorial_padding_right",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "memorial_padding_top",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_padding_bottom",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "display_padding_left",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "display_padding_right",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "display_padding_top",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "memorial_padding_bottom",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "memorial_padding_left",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "memorial_padding_right",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "memorial_padding_top",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
