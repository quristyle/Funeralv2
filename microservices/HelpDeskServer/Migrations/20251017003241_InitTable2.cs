using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class InitTable2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wbs_wbs_parentwbswbsrid",
                schema: "helpdesk",
                table: "wbs");

            migrationBuilder.RenameColumn(
                name: "parentwbswbsrid",
                schema: "helpdesk",
                table: "wbs",
                newName: "parentwbsid");

            migrationBuilder.RenameIndex(
                name: "IX_wbs_parentwbswbsrid",
                schema: "helpdesk",
                table: "wbs",
                newName: "IX_wbs_parentwbsid");

            migrationBuilder.AddForeignKey(
                name: "FK_wbs_wbs_parentwbsid",
                schema: "helpdesk",
                table: "wbs",
                column: "parentwbsid",
                principalSchema: "helpdesk",
                principalTable: "wbs",
                principalColumn: "wbsrid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wbs_wbs_parentwbsid",
                schema: "helpdesk",
                table: "wbs");

            migrationBuilder.RenameColumn(
                name: "parentwbsid",
                schema: "helpdesk",
                table: "wbs",
                newName: "parentwbswbsrid");

            migrationBuilder.RenameIndex(
                name: "IX_wbs_parentwbsid",
                schema: "helpdesk",
                table: "wbs",
                newName: "IX_wbs_parentwbswbsrid");

            migrationBuilder.AddForeignKey(
                name: "FK_wbs_wbs_parentwbswbsrid",
                schema: "helpdesk",
                table: "wbs",
                column: "parentwbswbsrid",
                principalSchema: "helpdesk",
                principalTable: "wbs",
                principalColumn: "wbsrid");
        }
    }
}
