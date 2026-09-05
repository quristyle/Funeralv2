using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDbComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "system_menus",
                schema: "scom",
                comment: "시스템 메뉴 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "roles",
                schema: "scom",
                comment: "사용자 권한/역할 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "role_menus",
                schema: "scom",
                comment: "역할 - 메뉴 세부 권한 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "role_accounts",
                schema: "scom",
                comment: "역할 - 사용자 계정 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "i18n_resources",
                schema: "scom",
                comment: "다국어 자원 정보를 관리하는 엔티티");

            migrationBuilder.AlterTable(
                name: "departments",
                schema: "scom",
                comment: "조직/부서 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "companies",
                schema: "scom",
                comment: "회사 엔티티");

            migrationBuilder.AlterTable(
                name: "common_codes",
                schema: "scom",
                comment: "다단계 공통코드 엔티티 (자가 참조 구조)");

            migrationBuilder.AlterTable(
                name: "common_code_groups",
                schema: "scom",
                comment: "공통코드 그룹 엔티티");

            migrationBuilder.AlterTable(
                name: "biz_select_configs",
                schema: "scom",
                comment: "비즈니스 콤보박스 설정 엔티티");

            migrationBuilder.AlterTable(
                name: "accounts",
                schema: "scom",
                comment: "계정 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "account_profile_details",
                schema: "scom",
                comment: "사용자 계정의 확장 속성 (이메일, 전화번호, 사진 등)");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                comment: "메뉴 유형 (catalog, menu, button 등)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "메뉴 제목",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "system_menus",
                type: "integer",
                nullable: false,
                comment: "상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "redirect",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "리다이렉트 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "pid",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "부모 메뉴 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                comment: "라우트 경로",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "order_no",
                schema: "scom",
                table: "system_menus",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                comment: "메뉴 이름 (다국어 키 또는 명칭)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "menu_visible_with_forbidden",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "권한 없을 때 메뉴 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "link",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "외부 링크 URL",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "keep_alive",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "페이지 캐싱(Keep-Alive) 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "iframe_src",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "Iframe 소스 URL",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "icon",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "메뉴 아이콘",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "hide_in_menu",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "메뉴 숨김 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "dom_cached",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "DOM 캐싱 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "component",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "컴포넌트 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "badge_type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "뱃지 유형 (dot 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "badge",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "뱃지 내용",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "authority",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "권한 목록 (콤마 구분)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "auth_code",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "권한 코드",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "affix_tab",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "탭 고정 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "roles",
                type: "integer",
                nullable: false,
                comment: "역할 상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "roles",
                type: "text",
                nullable: true,
                comment: "역할 설명 및 비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "roles",
                type: "text",
                nullable: false,
                comment: "역할 명칭",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                schema: "scom",
                table: "i18n_resources",
                type: "text",
                nullable: false,
                comment: "다국어 번역 값",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "locale",
                schema: "scom",
                table: "i18n_resources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "로케일 (예: ko, en-US)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                schema: "scom",
                table: "i18n_resources",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "다국어 키 (예: common.expandAll)",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "category",
                schema: "scom",
                table: "i18n_resources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "카테고리/모듈 (예: common, ui, system)",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "departments",
                type: "integer",
                nullable: false,
                comment: "부서 상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "departments",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                comment: "부서 설명 및 비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                comment: "상위 부서 ID (트리 구조 지원)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                comment: "부서 명칭",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                comment: "소속 회사 ID",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                comment: "상태 (1: 사용, 0: 미사용)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                comment: "부모 코드 ID (다단계 구조용 자가 참조)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "level",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                comment: "계층 레벨 (1, 2, 3...)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "is_leaf",
                schema: "scom",
                table: "common_codes",
                type: "boolean",
                nullable: false,
                comment: "최하위 노드 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "i18n_key",
                schema: "scom",
                table: "common_codes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "다국어 키 (선택 사항)",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "group_id",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: false,
                comment: "소속 그룹 ID",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "code_value",
                schema: "scom",
                table: "common_codes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "코드 값",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "code_name",
                schema: "scom",
                table: "common_codes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "코드 명칭",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "common_code_groups",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_code_groups",
                type: "text",
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_hierarchical",
                schema: "scom",
                table: "common_code_groups",
                type: "boolean",
                nullable: false,
                comment: "계층 구조 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "group_name",
                schema: "scom",
                table: "common_code_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "그룹 명칭",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "group_code",
                schema: "scom",
                table: "common_code_groups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "그룹 코드 (식별자, 예: AREA_CODE)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "user_name",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "사용자 실명 또는 닉네임",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: false,
                comment: "사용자 아이디 (로그인 아이디)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "real_name",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "사용자 실명 : 반드시 2글자 이상 입력되어야 함.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "password",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: false,
                comment: "암호화된 비밀번호",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "department_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "소속 부서 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "소속 회사 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "avatar_group_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "아바타 이미지 파일 그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "label",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: true,
                comment: "라벨 (회사, 개인, 집 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_primary",
                schema: "scom",
                table: "account_profile_details",
                type: "boolean",
                nullable: false,
                comment: "대표 여부 (여러 이메일 중 기본값 등)",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "detail_type",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: false,
                comment: "속성 유형 (Email, Phone, Fax, Photo, SNS 등)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: false,
                comment: "실제 데이터 값",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "system_menus",
                schema: "scom",
                oldComment: "시스템 메뉴 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "roles",
                schema: "scom",
                oldComment: "사용자 권한/역할 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "role_menus",
                schema: "scom",
                oldComment: "역할 - 메뉴 세부 권한 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "role_accounts",
                schema: "scom",
                oldComment: "역할 - 사용자 계정 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "i18n_resources",
                schema: "scom",
                oldComment: "다국어 자원 정보를 관리하는 엔티티");

            migrationBuilder.AlterTable(
                name: "departments",
                schema: "scom",
                oldComment: "조직/부서 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "companies",
                schema: "scom",
                oldComment: "회사 엔티티");

            migrationBuilder.AlterTable(
                name: "common_codes",
                schema: "scom",
                oldComment: "다단계 공통코드 엔티티 (자가 참조 구조)");

            migrationBuilder.AlterTable(
                name: "common_code_groups",
                schema: "scom",
                oldComment: "공통코드 그룹 엔티티");

            migrationBuilder.AlterTable(
                name: "biz_select_configs",
                schema: "scom",
                oldComment: "비즈니스 콤보박스 설정 엔티티");

            migrationBuilder.AlterTable(
                name: "accounts",
                schema: "scom",
                oldComment: "계정 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "account_profile_details",
                schema: "scom",
                oldComment: "사용자 계정의 확장 속성 (이메일, 전화번호, 사진 등)");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "메뉴 유형 (catalog, menu, button 등)");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "메뉴 제목");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "system_menus",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<string>(
                name: "redirect",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "리다이렉트 경로");

            migrationBuilder.AlterColumn<string>(
                name: "pid",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "부모 메뉴 ID");

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "라우트 경로");

            migrationBuilder.AlterColumn<int>(
                name: "order_no",
                schema: "scom",
                table: "system_menus",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "메뉴 이름 (다국어 키 또는 명칭)");

            migrationBuilder.AlterColumn<bool>(
                name: "menu_visible_with_forbidden",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "권한 없을 때 메뉴 표시 여부");

            migrationBuilder.AlterColumn<string>(
                name: "link",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "외부 링크 URL");

            migrationBuilder.AlterColumn<bool>(
                name: "keep_alive",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "페이지 캐싱(Keep-Alive) 여부");

            migrationBuilder.AlterColumn<string>(
                name: "iframe_src",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Iframe 소스 URL");

            migrationBuilder.AlterColumn<string>(
                name: "icon",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "메뉴 아이콘");

            migrationBuilder.AlterColumn<bool>(
                name: "hide_in_menu",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "메뉴 숨김 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "dom_cached",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "DOM 캐싱 여부");

            migrationBuilder.AlterColumn<string>(
                name: "component",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "컴포넌트 경로");

            migrationBuilder.AlterColumn<string>(
                name: "badge_type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "뱃지 유형 (dot 등)");

            migrationBuilder.AlterColumn<string>(
                name: "badge",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "뱃지 내용");

            migrationBuilder.AlterColumn<string>(
                name: "authority",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "권한 목록 (콤마 구분)");

            migrationBuilder.AlterColumn<string>(
                name: "auth_code",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "권한 코드");

            migrationBuilder.AlterColumn<bool>(
                name: "affix_tab",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "탭 고정 여부");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "roles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "역할 상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "roles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "역할 설명 및 비고");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "역할 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                schema: "scom",
                table: "i18n_resources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "다국어 번역 값");

            migrationBuilder.AlterColumn<string>(
                name: "locale",
                schema: "scom",
                table: "i18n_resources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "로케일 (예: ko, en-US)");

            migrationBuilder.AlterColumn<string>(
                name: "key",
                schema: "scom",
                table: "i18n_resources",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "다국어 키 (예: common.expandAll)");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                schema: "scom",
                table: "i18n_resources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "카테고리/모듈 (예: common, ui, system)");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "departments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "부서 상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "departments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "부서 설명 및 비고");

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "상위 부서 ID (트리 구조 지원)");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "부서 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 회사 ID");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "상태 (1: 사용, 0: 미사용)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "부모 코드 ID (다단계 구조용 자가 참조)");

            migrationBuilder.AlterColumn<int>(
                name: "level",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "계층 레벨 (1, 2, 3...)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_leaf",
                schema: "scom",
                table: "common_codes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "최하위 노드 여부");

            migrationBuilder.AlterColumn<string>(
                name: "i18n_key",
                schema: "scom",
                table: "common_codes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "다국어 키 (선택 사항)");

            migrationBuilder.AlterColumn<string>(
                name: "group_id",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "code_value",
                schema: "scom",
                table: "common_codes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "코드 값");

            migrationBuilder.AlterColumn<string>(
                name: "code_name",
                schema: "scom",
                table: "common_codes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "코드 명칭");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "common_code_groups",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_code_groups",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<bool>(
                name: "is_hierarchical",
                schema: "scom",
                table: "common_code_groups",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "계층 구조 여부");

            migrationBuilder.AlterColumn<string>(
                name: "group_name",
                schema: "scom",
                table: "common_code_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "그룹 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "group_code",
                schema: "scom",
                table: "common_code_groups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "그룹 코드 (식별자, 예: AREA_CODE)");

            migrationBuilder.AlterColumn<string>(
                name: "user_name",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "사용자 실명 또는 닉네임");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "사용자 아이디 (로그인 아이디)");

            migrationBuilder.AlterColumn<string>(
                name: "real_name",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "사용자 실명 : 반드시 2글자 이상 입력되어야 함.");

            migrationBuilder.AlterColumn<string>(
                name: "password",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "암호화된 비밀번호");

            migrationBuilder.AlterColumn<string>(
                name: "department_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "소속 부서 ID");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "소속 회사 ID");

            migrationBuilder.AlterColumn<string>(
                name: "avatar_group_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "아바타 이미지 파일 그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "label",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "라벨 (회사, 개인, 집 등)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_primary",
                schema: "scom",
                table: "account_profile_details",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "대표 여부 (여러 이메일 중 기본값 등)");

            migrationBuilder.AlterColumn<string>(
                name: "detail_type",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "속성 유형 (Email, Phone, Fax, Photo, SNS 등)");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "실제 데이터 값");
        }
    }
}
