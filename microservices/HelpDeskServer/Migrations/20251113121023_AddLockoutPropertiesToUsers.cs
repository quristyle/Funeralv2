using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddLockoutPropertiesToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "wbslink",
                schema: "helpdesk",
                comment: "WBS 항목 간의 연결(의존성)을 정의하는 엔티티");

            migrationBuilder.AlterTable(
                name: "project",
                schema: "helpdesk",
                comment: "프로젝트 엔티티",
                oldComment: "팀");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                schema: "helpdesk",
                table: "wbslink",
                type: "text",
                nullable: false,
                comment: "연결 타입 (e.g., \"0\" for finish-to-start)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "targetwbsid",
                schema: "helpdesk",
                table: "wbslink",
                type: "integer",
                nullable: false,
                comment: "타겟 WBS 항목 ID",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "sourcewbsid",
                schema: "helpdesk",
                table: "wbslink",
                type: "integer",
                nullable: false,
                comment: "소스 WBS 항목 ID",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "responsibleuserid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "책임자 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "qcuserid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "QA 담당자 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "qccheckdate",
                schema: "helpdesk",
                table: "wbs",
                type: "timestamp with time zone",
                nullable: true,
                comment: "QA 확인 일자",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "QA 확인일");

            migrationBuilder.AlterColumn<string>(
                name: "qccheck",
                schema: "helpdesk",
                table: "wbs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "QA 확인 여부 (e.g., \"Y\", \"N\")",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "QA 확인 여부");

            migrationBuilder.AlterColumn<int>(
                name: "projectid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "프로젝트 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "parentwbsid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "부모 WBS ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "managerid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "관리자 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "devcheck",
                schema: "helpdesk",
                table: "wbs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "개발 확인 여부 (e.g., \"Y\", \"N\")",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "개발 확인 여부");

            migrationBuilder.AlterColumn<int>(
                name: "customerid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "고객 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "customercompanyid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "고객사 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "cost",
                schema: "helpdesk",
                table: "wbs",
                type: "numeric",
                nullable: true,
                comment: "실제 투입 비용",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "실제 비용");

            migrationBuilder.AlterColumn<string>(
                name: "comments",
                schema: "helpdesk",
                table: "wbs",
                type: "text",
                nullable: true,
                comment: "작업 관련 코멘트/비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "작업 관련 코멘트");

            migrationBuilder.AlterColumn<int>(
                name: "builduserid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                comment: "개발자 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "budget",
                schema: "helpdesk",
                table: "wbs",
                type: "numeric",
                nullable: true,
                comment: "예산 (계획 비용)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "예상 비용");

            migrationBuilder.AlterColumn<int>(
                name: "companyid",
                schema: "helpdesk",
                table: "teamcompanies",
                type: "integer",
                nullable: false,
                comment: "고객사 ID",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "teamid",
                schema: "helpdesk",
                table: "teamcompanies",
                type: "integer",
                nullable: false,
                comment: "팀 ID",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "teamid",
                schema: "helpdesk",
                table: "project",
                type: "integer",
                nullable: true,
                comment: "담당 팀 ID (Foreign Key)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failedloginattempts",
                schema: "helpdesk",
                table: "customer",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "로그인 실패 횟수");

            migrationBuilder.AddColumn<DateTime>(
                name: "lockoutend",
                schema: "helpdesk",
                table: "customer",
                type: "timestamp with time zone",
                nullable: true,
                comment: "계정 잠금 종료 시간 (UTC)");

            migrationBuilder.AddColumn<int>(
                name: "failedloginattempts",
                schema: "helpdesk",
                table: "admin",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "로그인 실패 횟수");

            migrationBuilder.AddColumn<DateTime>(
                name: "lockoutend",
                schema: "helpdesk",
                table: "admin",
                type: "timestamp with time zone",
                nullable: true,
                comment: "계정 잠금 종료 시간 (UTC)");

            migrationBuilder.CreateTable(
                name: "pushmessage",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false, comment: "메시지 제목"),
                    body = table.Column<string>(type: "text", nullable: false, comment: "메시지 본문"),
                    url = table.Column<string>(type: "text", nullable: true, comment: "클릭 시 이동할 URL"),
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
                    table.PrimaryKey("PK_pushmessage", x => x.id);
                },
                comment: "발송된 푸시 메시지의 내용을 저장하는 엔티티");

            migrationBuilder.CreateTable(
                name: "pushnotificationlog",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    endpoint = table.Column<string>(type: "text", nullable: false, comment: "대상 구독 Endpoint"),
                    issuccess = table.Column<bool>(type: "boolean", nullable: false, comment: "발송 성공 여부"),
                    failurereason = table.Column<string>(type: "text", nullable: true, comment: "실패 시 이유 (예외 메시지)"),
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
                    table.PrimaryKey("PK_pushnotificationlog", x => x.id);
                },
                comment: "푸시 알림 발송 기록을 저장하는 엔티티");

            migrationBuilder.CreateTable(
                name: "pushsubscriptions",
                schema: "helpdesk",
                columns: table => new
                {
                    endpoint = table.Column<string>(type: "text", nullable: false, comment: "Push Service Endpoint URL (PK)"),
                    p256dh = table.Column<string>(type: "text", nullable: false, comment: "P256DH 키"),
                    auth = table.Column<string>(type: "text", nullable: false, comment: "인증 키"),
                    userid = table.Column<int>(type: "integer", nullable: false, comment: "사용자 ID (Admin.Id 또는 Customer.Id)"),
                    usertype = table.Column<string>(type: "text", nullable: false, comment: "사용자 타입 (\"Admin\" 또는 \"Customer\")")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pushsubscriptions", x => x.endpoint);
                },
                comment: "Web Push 구독 정보를 저장하는 엔티티");

            migrationBuilder.CreateTable(
                name: "userproperty",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false, comment: "사용자 ID (Admin.Id 또는 Customer.Id)"),
                    usertype = table.Column<string>(type: "text", nullable: false, comment: "사용자 타입 (\"Admin\" 또는 \"Customer\")"),
                    key = table.Column<string>(type: "text", nullable: false, comment: "속성 키 (예: \"receiveEmailNotifications\")"),
                    value = table.Column<string>(type: "text", nullable: false, comment: "속성 값 (예: \"true\", \"false\")"),
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
                    table.PrimaryKey("PK_userproperty", x => x.id);
                },
                comment: "사용자별 확장 속성을 저장하는 엔티티 (예: 알림 설정)");

            migrationBuilder.CreateTable(
                name: "pushmessagerecipient",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pushmessageid = table.Column<int>(type: "integer", nullable: false, comment: "PushMessage 외래 키"),
                    userid = table.Column<int>(type: "integer", nullable: false, comment: "수신자 사용자 ID"),
                    usertype = table.Column<string>(type: "text", nullable: false, comment: "수신자 사용자 타입 (\"admin\" 또는 \"customer\")"),
                    endpoint = table.Column<string>(type: "text", nullable: false, comment: "메시지가 발송된 구독 Endpoint"),
                    isdelivered = table.Column<bool>(type: "boolean", nullable: false, comment: "푸시 서비스에 성공적으로 전달되었는지 여부"),
                    deliveredat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "푸시 서비스에 전달된 시간"),
                    isread = table.Column<bool>(type: "boolean", nullable: false, comment: "사용자가 알림을 확인했는지 여부"),
                    readat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "사용자가 알림을 확인한 시간"),
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
                    table.PrimaryKey("PK_pushmessagerecipient", x => x.id);
                    table.ForeignKey(
                        name: "FK_pushmessagerecipient_pushmessage_pushmessageid",
                        column: x => x.pushmessageid,
                        principalSchema: "helpdesk",
                        principalTable: "pushmessage",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "특정 사용자에게 발송된 푸시 메시지의 수신 상태를 추적하는 엔티티");

            migrationBuilder.CreateIndex(
                name: "IX_pushmessagerecipient_pushmessageid",
                schema: "helpdesk",
                table: "pushmessagerecipient",
                column: "pushmessageid");

            migrationBuilder.CreateIndex(
                name: "IX_userproperty_userid_usertype_key",
                schema: "helpdesk",
                table: "userproperty",
                columns: new[] { "userid", "usertype", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pushmessagerecipient",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "pushnotificationlog",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "pushsubscriptions",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "userproperty",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "pushmessage",
                schema: "helpdesk");

            migrationBuilder.DropColumn(
                name: "failedloginattempts",
                schema: "helpdesk",
                table: "customer");

            migrationBuilder.DropColumn(
                name: "lockoutend",
                schema: "helpdesk",
                table: "customer");

            migrationBuilder.DropColumn(
                name: "failedloginattempts",
                schema: "helpdesk",
                table: "admin");

            migrationBuilder.DropColumn(
                name: "lockoutend",
                schema: "helpdesk",
                table: "admin");

            migrationBuilder.AlterTable(
                name: "wbslink",
                schema: "helpdesk",
                oldComment: "WBS 항목 간의 연결(의존성)을 정의하는 엔티티");

            migrationBuilder.AlterTable(
                name: "project",
                schema: "helpdesk",
                comment: "팀",
                oldComment: "프로젝트 엔티티");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                schema: "helpdesk",
                table: "wbslink",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "연결 타입 (e.g., \"0\" for finish-to-start)");

            migrationBuilder.AlterColumn<int>(
                name: "targetwbsid",
                schema: "helpdesk",
                table: "wbslink",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "타겟 WBS 항목 ID");

            migrationBuilder.AlterColumn<int>(
                name: "sourcewbsid",
                schema: "helpdesk",
                table: "wbslink",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "소스 WBS 항목 ID");

            migrationBuilder.AlterColumn<int>(
                name: "responsibleuserid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "책임자 ID (Foreign Key)");

            migrationBuilder.AlterColumn<int>(
                name: "qcuserid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "QA 담당자 ID (Foreign Key)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "qccheckdate",
                schema: "helpdesk",
                table: "wbs",
                type: "timestamp with time zone",
                nullable: true,
                comment: "QA 확인일",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "QA 확인 일자");

            migrationBuilder.AlterColumn<string>(
                name: "qccheck",
                schema: "helpdesk",
                table: "wbs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "QA 확인 여부",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "QA 확인 여부 (e.g., \"Y\", \"N\")");

            migrationBuilder.AlterColumn<int>(
                name: "projectid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "프로젝트 ID (Foreign Key)");

            migrationBuilder.AlterColumn<int>(
                name: "parentwbsid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "부모 WBS ID (Foreign Key)");

            migrationBuilder.AlterColumn<int>(
                name: "managerid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "관리자 ID (Foreign Key)");

            migrationBuilder.AlterColumn<string>(
                name: "devcheck",
                schema: "helpdesk",
                table: "wbs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "개발 확인 여부",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "개발 확인 여부 (e.g., \"Y\", \"N\")");

            migrationBuilder.AlterColumn<int>(
                name: "customerid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "고객 ID (Foreign Key)");

            migrationBuilder.AlterColumn<int>(
                name: "customercompanyid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "고객사 ID (Foreign Key)");

            migrationBuilder.AlterColumn<decimal>(
                name: "cost",
                schema: "helpdesk",
                table: "wbs",
                type: "numeric",
                nullable: true,
                comment: "실제 비용",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "실제 투입 비용");

            migrationBuilder.AlterColumn<string>(
                name: "comments",
                schema: "helpdesk",
                table: "wbs",
                type: "text",
                nullable: true,
                comment: "작업 관련 코멘트",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "작업 관련 코멘트/비고");

            migrationBuilder.AlterColumn<int>(
                name: "builduserid",
                schema: "helpdesk",
                table: "wbs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "개발자 ID (Foreign Key)");

            migrationBuilder.AlterColumn<decimal>(
                name: "budget",
                schema: "helpdesk",
                table: "wbs",
                type: "numeric",
                nullable: true,
                comment: "예상 비용",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "예산 (계획 비용)");

            migrationBuilder.AlterColumn<int>(
                name: "companyid",
                schema: "helpdesk",
                table: "teamcompanies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "고객사 ID");

            migrationBuilder.AlterColumn<int>(
                name: "teamid",
                schema: "helpdesk",
                table: "teamcompanies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "팀 ID");

            migrationBuilder.AlterColumn<int>(
                name: "teamid",
                schema: "helpdesk",
                table: "project",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "담당 팀 ID (Foreign Key)");
        }
    }
}
