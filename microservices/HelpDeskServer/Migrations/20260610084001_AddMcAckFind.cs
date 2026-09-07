using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMcAckFind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mc_ack_finds",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mc_modelsid = table.Column<int>(type: "integer", nullable: false),
                    startcalcarrow = table.Column<string>(type: "text", nullable: false),
                    startcalctarget = table.Column<string>(type: "text", nullable: false),
                    startcalcidx = table.Column<string>(type: "text", nullable: false),
                    startcalcvalue = table.Column<string>(type: "text", nullable: false),
                    startcalcequals = table.Column<string>(type: "text", nullable: false),
                    endcalcarrow = table.Column<string>(type: "text", nullable: false),
                    endcalctarget = table.Column<string>(type: "text", nullable: false),
                    endcalcidx = table.Column<string>(type: "text", nullable: false),
                    endcalcvalue = table.Column<string>(type: "text", nullable: false),
                    endcalcequals = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mc_ack_finds", x => x.id);
                    table.ForeignKey(
                        name: "FK_mc_ack_finds_mc_models_mc_modelsid",
                        column: x => x.mc_modelsid,
                        principalSchema: "helpdesk",
                        principalTable: "mc_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mc_ack_finds_mc_modelsid",
                schema: "helpdesk",
                table: "mc_ack_finds",
                column: "mc_modelsid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mc_ack_finds",
                schema: "helpdesk");
        }
    }
}
