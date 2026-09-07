using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseEntityAndTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "createdat",
                schema: "scom",
                table: "companies",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "businessnumber",
                schema: "scom",
                table: "companies",
                newName: "business_number");

            migrationBuilder.RenameColumn(
                name: "createdat",
                schema: "scom",
                table: "account_profile_details",
                newName: "created_at");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "system_menus",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "scom",
                table: "roles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "scom",
                table: "roles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "companies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "scom",
                table: "account_profile_details",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "scom",
                table: "system_menus");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "scom",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "scom",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "scom",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "scom",
                table: "account_profile_details");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "scom",
                table: "account_profile_details");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "scom",
                table: "account_profile_details");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "scom",
                table: "companies",
                newName: "createdat");

            migrationBuilder.RenameColumn(
                name: "business_number",
                schema: "scom",
                table: "companies",
                newName: "businessnumber");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "scom",
                table: "account_profile_details",
                newName: "createdat");
        }
    }
}
