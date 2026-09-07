using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_profile_details_accounts_accountid",
                schema: "scom",
                table: "account_profile_details");

            migrationBuilder.RenameColumn(
                name: "isprimary",
                schema: "scom",
                table: "account_profile_details",
                newName: "is_primary");

            migrationBuilder.RenameColumn(
                name: "detailtype",
                schema: "scom",
                table: "account_profile_details",
                newName: "detail_type");

            migrationBuilder.RenameColumn(
                name: "accountid",
                schema: "scom",
                table: "account_profile_details",
                newName: "account_id");

            migrationBuilder.RenameIndex(
                name: "IX_account_profile_details_accountid",
                schema: "scom",
                table: "account_profile_details",
                newName: "IX_account_profile_details_account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_profile_details_accounts_account_id",
                schema: "scom",
                table: "account_profile_details",
                column: "account_id",
                principalSchema: "scom",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_profile_details_accounts_account_id",
                schema: "scom",
                table: "account_profile_details");

            migrationBuilder.RenameColumn(
                name: "is_primary",
                schema: "scom",
                table: "account_profile_details",
                newName: "isprimary");

            migrationBuilder.RenameColumn(
                name: "detail_type",
                schema: "scom",
                table: "account_profile_details",
                newName: "detailtype");

            migrationBuilder.RenameColumn(
                name: "account_id",
                schema: "scom",
                table: "account_profile_details",
                newName: "accountid");

            migrationBuilder.RenameIndex(
                name: "IX_account_profile_details_account_id",
                schema: "scom",
                table: "account_profile_details",
                newName: "IX_account_profile_details_accountid");

            migrationBuilder.AddForeignKey(
                name: "FK_account_profile_details_accounts_accountid",
                schema: "scom",
                table: "account_profile_details",
                column: "accountid",
                principalSchema: "scom",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
