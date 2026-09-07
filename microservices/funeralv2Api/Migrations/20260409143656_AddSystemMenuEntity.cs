using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemMenuEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_menus",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    component = table.Column<string>(type: "text", nullable: true),
                    pid = table.Column<string>(type: "text", nullable: true),
                    redirect = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    auth_code = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    icon = table.Column<string>(type: "text", nullable: true),
                    order_no = table.Column<int>(type: "integer", nullable: false),
                    hide_in_menu = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_menus", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_menus",
                schema: "smfr");
        }
    }
}
