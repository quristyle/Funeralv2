using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    businessnumber = table.Column<string>(type: "text", nullable: true),
                    representative = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_departments_company_id",
                schema: "scom",
                table: "departments",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_department_id",
                schema: "scom",
                table: "accounts",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_departments_department_id",
                schema: "scom",
                table: "accounts",
                column: "department_id",
                principalSchema: "scom",
                principalTable: "departments",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_departments_companies_company_id",
                schema: "scom",
                table: "departments",
                column: "company_id",
                principalSchema: "scom",
                principalTable: "companies",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_departments_department_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_departments_companies_company_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropTable(
                name: "companies",
                schema: "scom");

            migrationBuilder.DropIndex(
                name: "IX_departments_company_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "IX_accounts_department_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "company_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "scom",
                table: "accounts");
        }
    }
}
