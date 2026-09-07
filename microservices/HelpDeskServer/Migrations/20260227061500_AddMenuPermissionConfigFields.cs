using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuPermissionConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ext1name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext2name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext3name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext4name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext5name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext6name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext7name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ext8name",
                schema: "helpdesk",
                table: "menu",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "usecreate",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "usedelete",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext1",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext2",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext3",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext4",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext5",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext6",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext7",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useext8",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useread",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "useupdate",
                schema: "helpdesk",
                table: "menu",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ext1name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext2name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext3name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext4name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext5name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext6name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext7name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "ext8name",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "usecreate",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "usedelete",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext1",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext2",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext3",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext4",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext5",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext6",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext7",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useext8",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useread",
                schema: "helpdesk",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "useupdate",
                schema: "helpdesk",
                table: "menu");
        }
    }
}
