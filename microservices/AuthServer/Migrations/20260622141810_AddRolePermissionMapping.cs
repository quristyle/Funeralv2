using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissionMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_accounts",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_accounts_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_accounts_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "scom",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_menus",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false),
                    menu_id = table.Column<string>(type: "text", nullable: false),
                    can_view = table.Column<bool>(type: "boolean", nullable: false),
                    can_search = table.Column<bool>(type: "boolean", nullable: false),
                    can_create = table.Column<bool>(type: "boolean", nullable: false),
                    can_delete = table.Column<bool>(type: "boolean", nullable: false),
                    can_update = table.Column<bool>(type: "boolean", nullable: false),
                    can_print = table.Column<bool>(type: "boolean", nullable: false),
                    can_excel = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust1 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust2 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust3 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust4 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust5 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust6 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust7 = table.Column<bool>(type: "boolean", nullable: false),
                    can_cust8 = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_menus", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_menus_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "scom",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_menus_system_menus_menu_id",
                        column: x => x.menu_id,
                        principalSchema: "scom",
                        principalTable: "system_menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_accounts_account_id",
                schema: "scom",
                table: "role_accounts",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_accounts_role_id_account_id",
                schema: "scom",
                table: "role_accounts",
                columns: new[] { "role_id", "account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_menu_id",
                schema: "scom",
                table: "role_menus",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_role_id_menu_id",
                schema: "scom",
                table: "role_menus",
                columns: new[] { "role_id", "menu_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_accounts",
                schema: "scom");

            migrationBuilder.DropTable(
                name: "role_menus",
                schema: "scom");
        }
    }
}
