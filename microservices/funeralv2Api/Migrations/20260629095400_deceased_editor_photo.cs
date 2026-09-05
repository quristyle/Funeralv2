using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class deceased_editor_photo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "memorial_edited_photo_file_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "memorial_edited_photo_url",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "memorial_edited_photo_file_id",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.DropColumn(
                name: "memorial_edited_photo_url",
                schema: "smfr",
                table: "deceaseds");
        }
    }
}
