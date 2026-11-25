using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    /// <remarks>스냅샷에 남아 있던 옛 차이(쓰지 않는 메뉴·역할 표, 주석 줄바꿈)는 이 마이그레이션에 싣지 않았다 — 칸 셋만 더한다.</remarks>
    public partial class AddRequestSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deletedat",
                schema: "helpdesk",
                table: "improvementrequest",
                type: "timestamp with time zone",
                nullable: true,
                comment: "지운 시각(UTC).");

            migrationBuilder.AddColumn<string>(
                name: "deletedby",
                schema: "helpdesk",
                table: "improvementrequest",
                type: "text",
                nullable: true,
                comment: "지운 사람(포털 아이디).");

            migrationBuilder.AddColumn<bool>(
                name: "isdel",
                schema: "helpdesk",
                table: "improvementrequest",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "지움 표시. 지운 글은 줄을 남기고 이 칸만 세운다 — 전역 걸름(AppDbContext)이 시스템관리자 길 말고는 모두 가린다.\n            줄과 첨부·그림·댓글을 실제로 지우는 것은 시스템관리자의 완전삭제(RequestPurger)뿐이다.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "deletedat", schema: "helpdesk", table: "improvementrequest");
            migrationBuilder.DropColumn(name: "deletedby", schema: "helpdesk", table: "improvementrequest");
            migrationBuilder.DropColumn(name: "isdel", schema: "helpdesk", table: "improvementrequest");
        }
    }
}
