using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class UnifyDeceasedStatusCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                comment: "장례 진행 상태. 허용 값은 DeceasedStatus 의 셋뿐이다 —\n            FUNERAL_IN_PROGRESS(진행중) · FUNERAL_DEPARTURE_COMPLETED(출상) · COMPLETED(종료).",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldComment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)");

            // 옛 코드값을 정본 셋으로 치환한다 (47번 문서 D-RS1).
            // IN_HOSPITAL → 진행중 · DISCHARGED → 출상 · SETTLEMENT_COMPLETED → 종료.
            // 화면과 서비스가 이제 정본만 검사하므로, 이 치환 없이는 옛 값이 걸러진다.
            migrationBuilder.Sql("""
                UPDATE smfr.deceaseds SET status = 'FUNERAL_IN_PROGRESS'          WHERE status = 'IN_HOSPITAL';
                UPDATE smfr.deceaseds SET status = 'FUNERAL_DEPARTURE_COMPLETED' WHERE status = 'DISCHARGED';
                UPDATE smfr.deceaseds SET status = 'COMPLETED'                    WHERE status = 'SETTLEMENT_COMPLETED';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                comment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldComment: "장례 진행 상태. 허용 값은 DeceasedStatus 의 셋뿐이다 —\n            FUNERAL_IN_PROGRESS(진행중) · FUNERAL_DEPARTURE_COMPLETED(출상) · COMPLETED(종료).");
        }
    }
}
