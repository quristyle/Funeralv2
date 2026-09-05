﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <summary>
    /// 회사·부서 단위 역할 매핑 테이블 추가 (scom.role_companies · scom.role_departments).
    ///
    /// <para>
    /// <b>자동 생성된 내용에서 이 두 테이블 것만 남겼다.</b> `biz_select_configs` 의
    /// param_path · service_code · static_params 컬럼도 함께 만들려 했는데,
    /// 그 컬럼들은 EF 를 거치지 않고 이미 DB 에 적용되어 있다(운영 API 응답으로 확인).
    /// 그대로 두면 "이미 있다" 로 실패한다. 앞선 AddMenuFavorites 와 같은 상황이다.
    /// </para>
    ///
    /// <para>
    /// 함께 생성된 Designer/스냅샷은 손대지 않았다. 그쪽은 실제 DB 모습과 맞으므로,
    /// 이 마이그레이션을 적용해 두면 다음 마이그레이션이 같은 것을 다시 만들려 하지 않는다.
    /// </para>
    /// </summary>
    public partial class AddRoleScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_companies",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false, comment: "연관된 역할 식별자 (ID)"),
                    company_id = table.Column<string>(type: "text", nullable: false, comment: "연관된 회사 식별자 (ID)"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_companies", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_companies_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "scom",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_companies_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "scom",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다.\n            \n             \n             역할은 세 단계로 줄 수 있다 — 회사 < 부서 < 사람 순으로 좁아지고,\n             좁은 쪽이 이긴다. 사람에게 직접 준 역할이 하나라도 있으면 그것만 쓰고,\n             없으면 그 사람의 부서, 그것도 없으면 회사의 역할을 쓴다\n             (자세한 규칙은 RoleAssignmentService.ResolveEffectiveRolesAsync 주석 참고).");

            migrationBuilder.CreateTable(
                name: "role_departments",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false, comment: "연관된 역할 식별자 (ID)"),
                    department_id = table.Column<string>(type: "text", nullable: false, comment: "연관된 부서 식별자 (ID)"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_departments", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_departments_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "scom",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되는 역할이다.\n            회사보다 좁고 사람보다 넓다( 주석 참고).");

            migrationBuilder.CreateIndex(
                name: "IX_role_companies_company_id",
                schema: "scom",
                table: "role_companies",
                column: "company_id");
            migrationBuilder.CreateIndex(
                name: "IX_role_companies_role_id_company_id",
                schema: "scom",
                table: "role_companies",
                columns: new[] { "role_id", "company_id" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_role_departments_department_id",
                schema: "scom",
                table: "role_departments",
                column: "department_id");
            migrationBuilder.CreateIndex(
                name: "IX_role_departments_role_id_department_id",
                schema: "scom",
                table: "role_departments",
                columns: new[] { "role_id", "department_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_departments",
                schema: "scom");

            migrationBuilder.DropTable(
                name: "role_companies",
                schema: "scom");
        }
    }
}
