using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddReportMailSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_mail_schedules",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "배치 이름. 목록에서 사람이 알아보는 글자다"),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "주기. ReportMailFrequency 의 값 하나 — DAILY · WEEKDAY ·\n            WEEKLY · MONTHLY."),
                    day_of_week = table.Column<int>(type: "integer", nullable: true, comment: "주간일 때 보낼 요일. 1(월) ~ 7(일) — ISO 순서다.\n            다른 주기에서는 비어 있다."),
                    day_of_month = table.Column<int>(type: "integer", nullable: true, comment: "월간일 때 보낼 날. 1 ~ 31.\n            그 달에 없는 날이면 말일에 보낸다(31 일을 고른 2월은 28·29일)."),
                    send_hour_kst = table.Column<int>(type: "integer", nullable: false, comment: "보낼 시각(시). 한국 벽시계다 — 머리말 참고"),
                    send_minute_kst = table.Column<int>(type: "integer", nullable: false, comment: "보낼 시각(분). 한국 벽시계다"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "켜져 있나. 꺼 두면 발송기가 건너뛴다"),
                    remark = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "메일 머리에 붙일 한마디. 비워도 된다"),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "마지막으로 보낸 때 (UTC). 한 번도 안 보냈으면 비어 있다"),
                    last_result = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "마지막 발송의 결과. 성공이면 받은 사람 수가, 실패면 까닭이 적힌다."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_mail_schedules", x => x.id);
                },
                comment: "보고서 메일 배치 하나 — 어떤 보고서를 · 어느 역할에게 ·\n             얼마나 자주 보낼지를 묶은 줄.");

            migrationBuilder.CreateTable(
                name: "report_mail_schedule_reports",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    schedule_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "어느 배치의 것인가"),
                    report_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "보고서 화면의 열쇠 (예: helpdesk.report.weekly)"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_mail_schedule_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_mail_schedule_reports_report_mail_schedules_schedule~",
                        column: x => x.schedule_id,
                        principalSchema: "scom",
                        principalTable: "report_mail_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "배치가 고른 보고서 하나. 가리키는 값은 메뉴 열쇠(route_key)다.");

            migrationBuilder.CreateTable(
                name: "report_mail_schedule_roles",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    schedule_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "어느 배치의 것인가"),
                    role_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "역할 식별자 (scom.roles.id)"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_mail_schedule_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_mail_schedule_roles_report_mail_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalSchema: "scom",
                        principalTable: "report_mail_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_report_mail_schedule_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "scom",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "배치를 받을 역할 하나.");

            migrationBuilder.CreateIndex(
                name: "IX_report_mail_schedule_reports_schedule_id_report_key",
                schema: "scom",
                table: "report_mail_schedule_reports",
                columns: new[] { "schedule_id", "report_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_mail_schedule_roles_role_id",
                schema: "scom",
                table: "report_mail_schedule_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_mail_schedule_roles_schedule_id_role_id",
                schema: "scom",
                table: "report_mail_schedule_roles",
                columns: new[] { "schedule_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_mail_schedules_is_active",
                schema: "scom",
                table: "report_mail_schedules",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_mail_schedule_reports",
                schema: "scom");

            migrationBuilder.DropTable(
                name: "report_mail_schedule_roles",
                schema: "scom");

            migrationBuilder.DropTable(
                name: "report_mail_schedules",
                schema: "scom");
        }
    }
}
