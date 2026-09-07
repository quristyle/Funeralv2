using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class RestoreBinarySampleAndTagSortNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sortno",
                schema: "jsini",
                table: "tag_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "fileid",
                schema: "jsini",
                table: "attachment",
                type: "text",
                nullable: true,
                comment: "FileServer 가 발급한 파일 아이디. 채워져 있으면 이 첨부는 FileServer 로 옮겨진 것이다.");

            migrationBuilder.AddColumn<DateTime>(
                name: "migratedat",
                schema: "jsini",
                table: "attachment",
                type: "timestamp with time zone",
                nullable: true,
                comment: "FileServer 로 옮긴 시각. 옮기지 않았으면 비어 있다.");

            migrationBuilder.CreateTable(
                name: "auth_user_links",
                schema: "jsini",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, comment: "매핑 식별자")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    authuserid = table.Column<string>(type: "text", nullable: false, comment: "AuthServer 계정 식별자 (scom.accounts.user_id, JWT 의 NameIdentifier 클레임)"),
                    usertype = table.Column<string>(type: "text", nullable: false, comment: "헬프데스크 계정 종류 — admin 또는 customer"),
                    helpdeskuserid = table.Column<int>(type: "integer", nullable: false, comment: "헬프데스크 내부 계정 ID (jsini.admin.id 또는 jsini.customer.id)"),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "매핑 생성 시각"),
                    createdby = table.Column<string>(type: "text", nullable: true, comment: "매핑을 만든 주체")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_user_links", x => x.id);
                },
                comment: "funeralv2(AuthServer) 계정과 헬프데스크 계정(Admin/Customer)을 잇는 매핑.\n            \n             헬프데스크는 원래 자체 계정(Admins/Customers)으로만 로그인했다. funeralv2 로 계정을 단일화하면서\n             AuthServer 가 발급한 토큰 하나로 헬프데스크 API 를 쓸 수 있어야 하는데,\n             기존 데이터(요청 작성자·담당자 등)가 모두 헬프데스크 내부 ID 를 참조하고 있어 그 ID 를 버릴 수 없다.\n             그래서 기존 테이블은 건드리지 않고 이 매핑 테이블만 추가해 두 체계를 연결한다.");

            migrationBuilder.CreateTable(
                name: "binarysample",
                schema: "jsini",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mc_modelsid = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modifiedby = table.Column<string>(type: "text", nullable: true),
                    modifiedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actionservice = table.Column<string>(type: "text", nullable: true),
                    menucontext = table.Column<string>(type: "text", nullable: true),
                    remoteaddr = table.Column<string>(type: "text", nullable: true),
                    remotemchineinfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_binarysample", x => x.id);
                    table.ForeignKey(
                        name: "FK_binarysample_mc_models_mc_modelsid",
                        column: x => x.mc_modelsid,
                        principalSchema: "jsini",
                        principalTable: "mc_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_binarysample_mc_modelsid",
                schema: "jsini",
                table: "binarysample",
                column: "mc_modelsid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_user_links",
                schema: "jsini");

            migrationBuilder.DropTable(
                name: "binarysample",
                schema: "jsini");

            migrationBuilder.DropColumn(
                name: "sortno",
                schema: "jsini",
                table: "tag_items");

            migrationBuilder.DropColumn(
                name: "fileid",
                schema: "jsini",
                table: "attachment");

            migrationBuilder.DropColumn(
                name: "migratedat",
                schema: "jsini",
                table: "attachment");
        }
    }
}
