using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarGroupIdToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_departments_department_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_departments_companies_company_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "IX_departments_company_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "IX_accounts_company_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_department_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avatar_group_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_departments_company_id_id",
                schema: "scom",
                table: "departments",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_company_id_department_id",
                schema: "scom",
                table: "accounts",
                columns: new[] { "company_id", "department_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_departments_company_id_department_id",
                schema: "scom",
                table: "accounts",
                columns: new[] { "company_id", "department_id" },
                principalSchema: "scom",
                principalTable: "departments",
                principalColumns: new[] { "company_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_departments_companies_company_id",
                schema: "scom",
                table: "departments",
                column: "company_id",
                principalSchema: "scom",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_departments_company_id_department_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_departments_companies_company_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_departments_company_id_id",
                schema: "scom",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "IX_accounts_company_id_department_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "avatar_group_id",
                schema: "scom",
                table: "accounts");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_departments_company_id",
                schema: "scom",
                table: "departments",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_company_id",
                schema: "scom",
                table: "accounts",
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
    }
}
