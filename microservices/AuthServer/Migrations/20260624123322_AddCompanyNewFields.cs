using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_detail",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approval_date",
                schema: "scom",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zip_code",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "address_detail",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "approval_date",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "short_name",
                schema: "scom",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "zip_code",
                schema: "scom",
                table: "companies");
        }
    }
}
