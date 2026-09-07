using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMCModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mc_models",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mc_name = table.Column<string>(type: "text", nullable: false),
                    startkey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mc_models", x => x.id);
                },
                comment: "특정 장비에 대한 정보");

            migrationBuilder.CreateTable(
                name: "parse_items",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mc_modelsid = table.Column<int>(type: "integer", nullable: false),
                    desc = table.Column<string>(type: "text", nullable: false),
                    ptype = table.Column<string>(type: "text", nullable: false),
                    keyidx = table.Column<int>(type: "integer", nullable: false),
                    keys = table.Column<byte[]>(type: "bytea", nullable: false),
                    blocparsetype = table.Column<string>(type: "text", nullable: false),
                    blocparselength = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parse_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_parse_items_mc_models_mc_modelsid",
                        column: x => x.mc_modelsid,
                        principalSchema: "helpdesk",
                        principalTable: "mc_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "하나의 전문에 대한 정보");

            migrationBuilder.CreateTable(
                name: "tag_items",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parseitemid = table.Column<int>(type: "integer", nullable: false),
                    desc = table.Column<string>(type: "text", nullable: false),
                    tagidx = table.Column<int>(type: "integer", nullable: false),
                    taglength = table.Column<int>(type: "integer", nullable: false),
                    datatype = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_tag_items_parse_items_parseitemid",
                        column: x => x.parseitemid,
                        principalSchema: "helpdesk",
                        principalTable: "parse_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "하나의 전문에 소속된 테그 정보");

            migrationBuilder.CreateIndex(
                name: "IX_parse_items_mc_modelsid",
                schema: "helpdesk",
                table: "parse_items",
                column: "mc_modelsid");

            migrationBuilder.CreateIndex(
                name: "IX_tag_items_parseitemid",
                schema: "helpdesk",
                table: "tag_items",
                column: "parseitemid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tag_items",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "parse_items",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "mc_models",
                schema: "helpdesk");
        }
    }
}
