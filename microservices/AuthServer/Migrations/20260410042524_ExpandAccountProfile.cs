using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAccountProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                schema: "scom",
                table: "accounts");

            migrationBuilder.CreateTable(
                name: "account_profile_details",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    accountid = table.Column<string>(type: "text", nullable: false),
                    detailtype = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    isprimary = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    remark = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_profile_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_profile_details_accounts_accountid",
                        column: x => x.accountid,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_profile_details_accountid",
                schema: "scom",
                table: "account_profile_details",
                column: "accountid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_profile_details",
                schema: "scom");

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true);
        }
    }
}
