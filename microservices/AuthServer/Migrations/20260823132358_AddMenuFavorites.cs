﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <summary>
    /// 사용자별 즐겨찾기 메뉴 테이블(scom.menu_favorites) 추가.
    ///
    /// <para>
    /// <b>자동 생성된 내용에서 이 테이블 것만 남겼다.</b> 마지막 마이그레이션
    /// (20260707115423_UpdateAllComments) 이후의 스키마 변경 일부가 EF 를 거치지 않고
    /// docs/sql 스크립트로 직접 적용되어, 모델 스냅샷이 실제 DB 보다 뒤처져 있었다.
    /// 그래서 `migrations add` 가 이미 존재하는 것들까지 만들려고 했다 —
    /// system_menus 의 권한 컬럼 23개(use_view … cust8_name) 와 notices · notice_files 테이블이다.
    /// 그대로 두면 적용 시 "이미 있다" 로 실패한다.
    /// </para>
    ///
    /// <para>
    /// 함께 생성된 Designer/스냅샷은 손대지 않았다. 그쪽은 <b>실제 DB 모습</b>과 맞으므로,
    /// 이 마이그레이션을 적용해 두면 다음 마이그레이션이 같은 것을 다시 만들려 하지 않는다.
    /// </para>
    /// </summary>
    public partial class AddMenuFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_favorites",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<string>(type: "text", nullable: false, comment: "연관된 사용자 계정 식별자 (ID) — scom.accounts.id"),
                    menu_id = table.Column<string>(type: "text", nullable: false, comment: "즐겨찾기한 메뉴 식별자 (ID) — scom.system_menus.id"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, comment: "사이드바 즐겨찾기 묶음에서의 표시 순서 (작은 값이 위)"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_favorites", x => x.id);
                    table.ForeignKey(
                        name: "FK_menu_favorites_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_menu_favorites_system_menus_menu_id",
                        column: x => x.menu_id,
                        principalSchema: "scom",
                        principalTable: "system_menus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "사용자별 즐겨찾기 메뉴 엔티티 클래스 (계정 - 메뉴 N:M 관계 해소용 매핑 테이블)\n            \n             \n             자주 쓰는 화면을 사용자가 직접 모아 두는 곳이다. 탭을 오른쪽 눌러 추가하고,\n             왼쪽 사이드바 맨 위 '즐겨찾기' 묶음에서 바로 열 수 있다.\n             메뉴를 경로가 아니라 식별자로 가리킨다. 화면(탭)이 아는 것은 경로뿐이라\n             등록·해제 API 는 경로를 받지만, 저장할 때는 scom.system_menus 를 찾아\n             그 식별자를 넣는다. 경로를 그대로 저장하면 메뉴 관리에서 경로를 고치는 순간\n             즐겨찾기가 아무 곳도 가리키지 않는 값으로 조용히 남는다.\n             \n             제목·아이콘은 여기 담지 않는다. 메뉴 쪽 값이 정본이고, 읽을 때 함께 조회한다.\n             베껴 두면 메뉴 제목을 고쳤을 때 즐겨찾기만 옛 이름으로 남는다.");

            migrationBuilder.CreateIndex(
                name: "IX_menu_favorites_account_id_menu_id",
                schema: "scom",
                table: "menu_favorites",
                columns: new[] { "account_id", "menu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_menu_favorites_menu_id",
                schema: "scom",
                table: "menu_favorites",
                column: "menu_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_favorites",
                schema: "scom");
        }
    }
}
