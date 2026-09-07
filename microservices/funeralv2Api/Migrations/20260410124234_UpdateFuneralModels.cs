using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFuneralModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "system_menus",
                schema: "smfr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "smfr");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    password = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    parent_id = table.Column<string>(type: "text", nullable: true),
                    remark = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    remark = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_menus",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    auth_code = table.Column<string>(type: "text", nullable: true),
                    component = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    hide_in_menu = table.Column<bool>(type: "boolean", nullable: false),
                    icon = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    order_no = table.Column<int>(type: "integer", nullable: false),
                    pid = table.Column<string>(type: "text", nullable: true),
                    path = table.Column<string>(type: "text", nullable: false),
                    redirect = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_menus", x => x.id);
                });
        }
    }
}
