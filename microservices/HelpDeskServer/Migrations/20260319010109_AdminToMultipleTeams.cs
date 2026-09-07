using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AdminToMultipleTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_admin_team_teamid",
                schema: "helpdesk",
                table: "admin");

            migrationBuilder.DropIndex(
                name: "IX_admin_teamid",
                schema: "helpdesk",
                table: "admin");

            migrationBuilder.DropColumn(
                name: "teamid",
                schema: "helpdesk",
                table: "admin");

            migrationBuilder.CreateTable(
                name: "adminteams",
                schema: "helpdesk",
                columns: table => new
                {
                    adminid = table.Column<int>(type: "integer", nullable: false, comment: "관리자 ID"),
                    teamid = table.Column<int>(type: "integer", nullable: false, comment: "팀 ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adminteams", x => new { x.adminid, x.teamid });
                    table.ForeignKey(
                        name: "FK_adminteams_admin_adminid",
                        column: x => x.adminid,
                        principalSchema: "helpdesk",
                        principalTable: "admin",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adminteams_team_teamid",
                        column: x => x.teamid,
                        principalSchema: "helpdesk",
                        principalTable: "team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "관리자-팀 매핑 (N:N 관계)");

            migrationBuilder.CreateIndex(
                name: "IX_adminteams_teamid",
                schema: "helpdesk",
                table: "adminteams",
                column: "teamid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adminteams",
                schema: "helpdesk");

            migrationBuilder.AddColumn<int>(
                name: "teamid",
                schema: "helpdesk",
                table: "admin",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "팀 ID");

            migrationBuilder.CreateIndex(
                name: "IX_admin_teamid",
                schema: "helpdesk",
                table: "admin",
                column: "teamid");

            migrationBuilder.AddForeignKey(
                name: "FK_admin_team_teamid",
                schema: "helpdesk",
                table: "admin",
                column: "teamid",
                principalSchema: "helpdesk",
                principalTable: "team",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
