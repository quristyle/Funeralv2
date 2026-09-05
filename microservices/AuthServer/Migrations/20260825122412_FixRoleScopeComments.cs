﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <summary>
    /// role_companies · role_departments 의 테이블 주석을 정정한다.
    ///
    /// <para>
    /// 역할 적용 규칙을 "좁은 쪽이 이긴다(덮어쓰기)" 에서 <b>"셋을 모두 합친다"</b> 로
    /// 바꾸면서, 엔티티 주석에서 만들어지는 DB 주석도 함께 맞춘다.
    /// </para>
    ///
    /// <para>
    /// <b>주석만 바꾼다.</b> 자동 생성분에는 다른 작업에서 EF 를 거치지 않고 진행 중인
    /// 변경(accounts 의 접속 기록 컬럼, FAQ·QnA 테이블)이 섞여 있어 전부 걷어냈다.
    /// 그쪽은 그 작업이 자기 마이그레이션으로 처리할 몫이다.
    /// </para>
    /// </summary>
    public partial class FixRoleScopeComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "role_departments",
                schema: "scom",
                comment: "역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되는 역할이다.\n            상위 부서에 걸린 역할도 함께 물려받는다( 주석 참고).",
                oldComment: "역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되는 역할이다.\n            회사보다 좁고 사람보다 넓다( 주석 참고).");

            migrationBuilder.AlterTable(
                name: "role_companies",
                schema: "scom",
                comment: "역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다.\n            \n             \n             역할은 세 단계로 줄 수 있다 — 회사 · 부서 · 사람. 셋을 모두 합쳐 적용된다.\n             어느 한 단계가 다른 단계를 덮어쓰지 않는다\n             (자세한 규칙은 RoleAssignmentService.ResolveEffectiveRolesAsync 주석 참고).",
                oldComment: "역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다.\n            \n             \n             역할은 세 단계로 줄 수 있다 — 회사 < 부서 < 사람 순으로 좁아지고,\n             좁은 쪽이 이긴다. 사람에게 직접 준 역할이 하나라도 있으면 그것만 쓰고,\n             없으면 그 사람의 부서, 그것도 없으면 회사의 역할을 쓴다\n             (자세한 규칙은 RoleAssignmentService.ResolveEffectiveRolesAsync 주석 참고).");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "role_departments",
                schema: "scom",
                oldComment: "역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되는 역할이다.\n            상위 부서에 걸린 역할도 함께 물려받는다( 주석 참고).",
                comment: "역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되는 역할이다.\n            회사보다 좁고 사람보다 넓다( 주석 참고).");

            migrationBuilder.AlterTable(
                name: "role_companies",
                schema: "scom",
                oldComment: "역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다.\n            \n             \n             역할은 세 단계로 줄 수 있다 — 회사 · 부서 · 사람. 셋을 모두 합쳐 적용된다.\n             어느 한 단계가 다른 단계를 덮어쓰지 않는다\n             (자세한 규칙은 RoleAssignmentService.ResolveEffectiveRolesAsync 주석 참고).",
                comment: "역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다.\n            \n             \n             역할은 세 단계로 줄 수 있다 — 회사 < 부서 < 사람 순으로 좁아지고,\n             좁은 쪽이 이긴다. 사람에게 직접 준 역할이 하나라도 있으면 그것만 쓰고,\n             없으면 그 사람의 부서, 그것도 없으면 회사의 역할을 쓴다\n             (자세한 규칙은 RoleAssignmentService.ResolveEffectiveRolesAsync 주석 참고).");

        }
    }
}
