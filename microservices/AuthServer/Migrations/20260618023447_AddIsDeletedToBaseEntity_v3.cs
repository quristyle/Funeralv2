using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToBaseEntity_v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "system_menus",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "i18n_resources",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "i18n_resources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "departments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "departments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

//            migrationBuilder.AddColumn<string>(
//                name: "company_id",
//                schema: "scom",
//                table: "accounts",
//                type: "text",
//                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "account_profile_details",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "scom",
                table: "account_profile_details",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "common_code_groups",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    group_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    group_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_hierarchical = table.Column<bool>(type: "boolean", nullable: false),
                    remark = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_common_code_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "common_codes",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    group_id = table.Column<string>(type: "text", nullable: false),
                    parent_id = table.Column<string>(type: "text", nullable: true),
                    code_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    i18n_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    is_leaf = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_common_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_common_codes_common_code_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "scom",
                        principalTable: "common_code_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_common_codes_common_codes_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "scom",
                        principalTable: "common_codes",
                        principalColumn: "id");
                });

//            migrationBuilder.CreateIndex(
//                name: "IX_accounts_company_id",
//                schema: "scom",
//                table: "accounts",
//                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_common_codes_group_id",
                schema: "scom",
                table: "common_codes",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_common_codes_parent_id",
                schema: "scom",
                table: "common_codes",
                column: "parent_id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_accounts_companies_company_id",
//                schema: "scom",
//                table: "accounts",
//                column: "company_id",
//                principalSchema: "scom",
//                principalTable: "companies",
//                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
//            migrationBuilder.DropForeignKey(
//                name: "FK_accounts_companies_company_id",
//                schema: "scom",
//                table: "accounts");

            migrationBuilder.DropTable(
                name: "common_codes",
                schema: "scom");

            migrationBuilder.DropTable(
                name: "common_code_groups",
                schema: "scom");

//            migrationBuilder.DropIndex(
//                name: "IX_accounts_company_id",
//                schema: "scom",
//                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "i18n_resources");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "companies");

//            migrationBuilder.DropColumn(
//                name: "company_id",
//                schema: "scom",
//                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "scom",
                table: "account_profile_details");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "system_menus",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "i18n_resources",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "companies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "account_profile_details",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
