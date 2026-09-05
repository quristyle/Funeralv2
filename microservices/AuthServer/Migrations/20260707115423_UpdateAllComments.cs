using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAllComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "role_menus",
                schema: "scom",
                comment: "역할 - 메뉴 세부 권한 매핑 엔티티 클래스",
                oldComment: "역할 - 메뉴 세부 권한 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "role_accounts",
                schema: "scom",
                comment: "역할 - 사용자 계정 매핑 엔티티 클래스 (N:M 관계 해소용 매핑 테이블)",
                oldComment: "역할 - 사용자 계정 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "i18n_resources",
                schema: "scom",
                comment: "다국어 리소스 정보를 관리하는 엔티티 클래스",
                oldComment: "다국어 자원 정보를 관리하는 엔티티");

            migrationBuilder.AlterTable(
                name: "companies",
                schema: "scom",
                comment: "회사 엔티티 클래스",
                oldComment: "회사 엔티티");

            migrationBuilder.AlterTable(
                name: "common_codes",
                schema: "scom",
                comment: "다단계 공통코드 엔티티 클래스 (자가 참조 구조)",
                oldComment: "다단계 공통코드 엔티티 (자가 참조 구조)");

            migrationBuilder.AlterTable(
                name: "common_code_groups",
                schema: "scom",
                comment: "공통코드 그룹 엔티티 클래스",
                oldComment: "공통코드 그룹 엔티티");

            migrationBuilder.AlterTable(
                name: "biz_select_configs",
                schema: "scom",
                comment: "비즈니스 콤보박스 설정 엔티티 클래스",
                oldComment: "비즈니스 콤보박스 설정 엔티티");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                comment: "메뉴 유형 (catalog, menu, button 등, 기본값: menu)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "메뉴 유형 (catalog, menu, button 등)");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "메뉴 제목 (화면에 표시할 텍스트)",
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
                comment: "메뉴 사용 상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<string>(
                name: "redirect",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "리다이렉트할 경로 (URL)",
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
                comment: "부모 메뉴 식별자 (ID)",
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
                comment: "라우트 경로 (URL)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "라우트 경로");

            migrationBuilder.AlterColumn<bool>(
                name: "menu_visible_with_forbidden",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "권한이 없을 때 메뉴 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "권한 없을 때 메뉴 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "keep_alive",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "페이지 캐싱(Keep-Alive) 적용 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "페이지 캐싱(Keep-Alive) 여부");

            migrationBuilder.AlterColumn<string>(
                name: "iframe_src",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "Iframe 소스 URL (웹뷰용)",
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
                comment: "메뉴 아이콘 명칭 (예: AntDesign 등)",
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
                comment: "메뉴 표시 숨김 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "메뉴 숨김 여부");

            migrationBuilder.AlterColumn<string>(
                name: "component",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "프론트엔드 컴포넌트 파일 경로",
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
                comment: "뱃지 유형 (예: dot 등)",
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
                comment: "뱃지에 표시할 텍스트 내용",
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
                comment: "허용 권한 목록 (콤마 구분)",
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
                comment: "권한 식별 코드",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "권한 코드");

            migrationBuilder.AlterColumn<string>(
                name: "role_id",
                schema: "scom",
                table: "role_menus",
                type: "text",
                nullable: false,
                comment: "연관된 역할 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "menu_id",
                schema: "scom",
                table: "role_menus",
                type: "text",
                nullable: false,
                comment: "연관된 시스템 메뉴 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "can_view",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "조회(보기) 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_update",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "수정(업데이트) 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_search",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "검색 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_print",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "인쇄 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_excel",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "엑셀 출력 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_delete",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "삭제 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust8",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 8 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust7",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 7 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust6",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 6 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust5",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 5 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust4",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 4 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust3",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 3 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust2",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 2 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust1",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "사용자 정의 권한 1 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "can_create",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                comment: "생성(등록) 권한 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "role_id",
                schema: "scom",
                table: "role_accounts",
                type: "text",
                nullable: false,
                comment: "연관된 역할 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "account_id",
                schema: "scom",
                table: "role_accounts",
                type: "text",
                nullable: false,
                comment: "연관된 사용자 계정 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                schema: "scom",
                table: "i18n_resources",
                type: "text",
                nullable: false,
                comment: "다국어 번역 결과 텍스트 값",
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
                comment: "로케일 설정 (예: ko, en-US)",
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
                comment: "다국어 리소스 키 (예: common.expandAll)",
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
                comment: "카테고리 또는 모듈 구분 (예: common, ui, system)",
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
                comment: "부서 사용 상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "부서 상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                comment: "상위 부서 식별자 (ID) (트리 구조 지원)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "상위 부서 ID (트리 구조 지원)");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                comment: "소속 회사 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 회사 ID");

            migrationBuilder.AlterColumn<string>(
                name: "zip_code",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "우편번호",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "companies",
                type: "integer",
                nullable: false,
                comment: "회사 사용 상태 (1: 활성, 0: 비활성)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "companies",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "회사 약칭",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "representative",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "대표자명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: false,
                comment: "회사 명칭",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "business_number",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "사업자 등록 번호",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "approval_date",
                schema: "scom",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true,
                comment: "회사 승인 일자",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_detail",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "회사 상세 주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                comment: "회사 주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                comment: "사용 상태 (1: 사용, 0: 미사용)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "상태 (1: 사용, 0: 미사용)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
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
                comment: "부모 코드 식별자 (ID) (다단계 구조용 자가 참조)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "부모 코드 ID (다단계 구조용 자가 참조)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_leaf",
                schema: "scom",
                table: "common_codes",
                type: "boolean",
                nullable: false,
                comment: "최하위 자식 노드 여부",
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
                comment: "다국어 리소스 키 (선택 사항)",
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
                comment: "소속 공통코드 그룹 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_code_groups",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<string>(
                name: "group_code",
                schema: "scom",
                table: "common_code_groups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "그룹 코드 (예: AREA_CODE)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "그룹 코드 (식별자, 예: AREA_CODE)");

            migrationBuilder.AlterColumn<string>(
                name: "value_field",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                comment: "실제 서버로 전송할 값에 해당하는 필드명",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "result_path",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: true,
                comment: "API 응답 JSON 내에서 실제 배열/목록이 위치하는 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "processor_type",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: true,
                comment: "데이터를 파싱 및 가공할 처리기(Processor) 유형",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "label_field",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                comment: "화면에 표시할 텍스트에 해당하는 필드명",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "http_method",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                comment: "HTTP 메서드 (예: GET, POST 등, 기본값: GET)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "biz_type",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                comment: "비즈니스 유형 (예: 계정구분, 권한그룹 등 특정 콤보박스가 사용할 도메인 유형)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "api_url",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                comment: "데이터를 조회할 API 주소",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "real_name",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "사용자 실명 (반드시 2글자 이상 입력되어야 함)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "사용자 실명 : 반드시 2글자 이상 입력되어야 함.");

            migrationBuilder.AlterColumn<string>(
                name: "department_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "소속 부서 식별자 (ID)",
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
                comment: "소속 회사 식별자 (ID)",
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
                comment: "아바타 이미지 파일 그룹 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "아바타 이미지 파일 그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "account_id",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: false,
                comment: "연관된 사용자 계정 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "role_menus",
                schema: "scom",
                comment: "역할 - 메뉴 세부 권한 매핑 엔티티",
                oldComment: "역할 - 메뉴 세부 권한 매핑 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "role_accounts",
                schema: "scom",
                comment: "역할 - 사용자 계정 매핑 엔티티",
                oldComment: "역할 - 사용자 계정 매핑 엔티티 클래스 (N:M 관계 해소용 매핑 테이블)");

            migrationBuilder.AlterTable(
                name: "i18n_resources",
                schema: "scom",
                comment: "다국어 자원 정보를 관리하는 엔티티",
                oldComment: "다국어 리소스 정보를 관리하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "companies",
                schema: "scom",
                comment: "회사 엔티티",
                oldComment: "회사 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "common_codes",
                schema: "scom",
                comment: "다단계 공통코드 엔티티 (자가 참조 구조)",
                oldComment: "다단계 공통코드 엔티티 클래스 (자가 참조 구조)");

            migrationBuilder.AlterTable(
                name: "common_code_groups",
                schema: "scom",
                comment: "공통코드 그룹 엔티티",
                oldComment: "공통코드 그룹 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "biz_select_configs",
                schema: "scom",
                comment: "비즈니스 콤보박스 설정 엔티티",
                oldComment: "비즈니스 콤보박스 설정 엔티티 클래스");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                comment: "메뉴 유형 (catalog, menu, button 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "메뉴 유형 (catalog, menu, button 등, 기본값: menu)");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "메뉴 제목",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "메뉴 제목 (화면에 표시할 텍스트)");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "system_menus",
                type: "integer",
                nullable: false,
                comment: "상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "메뉴 사용 상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<string>(
                name: "redirect",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "리다이렉트 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "리다이렉트할 경로 (URL)");

            migrationBuilder.AlterColumn<string>(
                name: "pid",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "부모 메뉴 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "부모 메뉴 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: false,
                comment: "라우트 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "라우트 경로 (URL)");

            migrationBuilder.AlterColumn<bool>(
                name: "menu_visible_with_forbidden",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "권한 없을 때 메뉴 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "권한이 없을 때 메뉴 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "keep_alive",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "페이지 캐싱(Keep-Alive) 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "페이지 캐싱(Keep-Alive) 적용 여부");

            migrationBuilder.AlterColumn<string>(
                name: "iframe_src",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "Iframe 소스 URL",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Iframe 소스 URL (웹뷰용)");

            migrationBuilder.AlterColumn<string>(
                name: "icon",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "메뉴 아이콘",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "메뉴 아이콘 명칭 (예: AntDesign 등)");

            migrationBuilder.AlterColumn<bool>(
                name: "hide_in_menu",
                schema: "scom",
                table: "system_menus",
                type: "boolean",
                nullable: false,
                comment: "메뉴 숨김 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "메뉴 표시 숨김 여부");

            migrationBuilder.AlterColumn<string>(
                name: "component",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "컴포넌트 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "프론트엔드 컴포넌트 파일 경로");

            migrationBuilder.AlterColumn<string>(
                name: "badge_type",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "뱃지 유형 (dot 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "뱃지 유형 (예: dot 등)");

            migrationBuilder.AlterColumn<string>(
                name: "badge",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "뱃지 내용",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "뱃지에 표시할 텍스트 내용");

            migrationBuilder.AlterColumn<string>(
                name: "authority",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "권한 목록 (콤마 구분)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "허용 권한 목록 (콤마 구분)");

            migrationBuilder.AlterColumn<string>(
                name: "auth_code",
                schema: "scom",
                table: "system_menus",
                type: "text",
                nullable: true,
                comment: "권한 코드",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "권한 식별 코드");

            migrationBuilder.AlterColumn<string>(
                name: "role_id",
                schema: "scom",
                table: "role_menus",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "연관된 역할 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "menu_id",
                schema: "scom",
                table: "role_menus",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "연관된 시스템 메뉴 식별자 (ID)");

            migrationBuilder.AlterColumn<bool>(
                name: "can_view",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "조회(보기) 권한 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_update",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "수정(업데이트) 권한 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_search",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "검색 권한 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_print",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "인쇄 권한 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_excel",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "엑셀 출력 권한 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_delete",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "삭제 권한 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust8",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 8 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust7",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 7 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust6",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 6 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust5",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 5 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust4",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 4 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust3",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 3 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust2",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 2 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_cust1",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "사용자 정의 권한 1 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "can_create",
                schema: "scom",
                table: "role_menus",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "생성(등록) 권한 여부");

            migrationBuilder.AlterColumn<string>(
                name: "role_id",
                schema: "scom",
                table: "role_accounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "연관된 역할 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "account_id",
                schema: "scom",
                table: "role_accounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "연관된 사용자 계정 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                schema: "scom",
                table: "i18n_resources",
                type: "text",
                nullable: false,
                comment: "다국어 번역 값",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "다국어 번역 결과 텍스트 값");

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
                oldMaxLength: 20,
                oldComment: "로케일 설정 (예: ko, en-US)");

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
                oldMaxLength: 200,
                oldComment: "다국어 리소스 키 (예: common.expandAll)");

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
                oldNullable: true,
                oldComment: "카테고리 또는 모듈 구분 (예: common, ui, system)");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "departments",
                type: "integer",
                nullable: false,
                comment: "부서 상태 (0: 비활성, 1: 활성)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "부서 사용 상태 (0: 비활성, 1: 활성)");

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: true,
                comment: "상위 부서 ID (트리 구조 지원)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "상위 부서 식별자 (ID) (트리 구조 지원)");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "departments",
                type: "text",
                nullable: false,
                comment: "소속 회사 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 회사 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "zip_code",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "우편번호");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "companies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "회사 사용 상태 (1: 활성, 0: 비활성)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "scom",
                table: "companies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "회사 약칭");

            migrationBuilder.AlterColumn<string>(
                name: "representative",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "대표자명");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "회사 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "business_number",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "사업자 등록 번호");

            migrationBuilder.AlterColumn<DateTime>(
                name: "approval_date",
                schema: "scom",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "회사 승인 일자");

            migrationBuilder.AlterColumn<string>(
                name: "address_detail",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "회사 상세 주소");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "scom",
                table: "companies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "회사 주소");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "scom",
                table: "common_codes",
                type: "integer",
                nullable: false,
                comment: "상태 (1: 사용, 0: 미사용)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "사용 상태 (1: 사용, 0: 미사용)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "parent_id",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: true,
                comment: "부모 코드 ID (다단계 구조용 자가 참조)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "부모 코드 식별자 (ID) (다단계 구조용 자가 참조)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_leaf",
                schema: "scom",
                table: "common_codes",
                type: "boolean",
                nullable: false,
                comment: "최하위 노드 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "최하위 자식 노드 여부");

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
                oldNullable: true,
                oldComment: "다국어 리소스 키 (선택 사항)");

            migrationBuilder.AlterColumn<string>(
                name: "group_id",
                schema: "scom",
                table: "common_codes",
                type: "text",
                nullable: false,
                comment: "소속 그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 공통코드 그룹 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "common_code_groups",
                type: "text",
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

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
                oldMaxLength: 50,
                oldComment: "그룹 코드 (예: AREA_CODE)");

            migrationBuilder.AlterColumn<string>(
                name: "value_field",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "실제 서버로 전송할 값에 해당하는 필드명");

            migrationBuilder.AlterColumn<string>(
                name: "result_path",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "API 응답 JSON 내에서 실제 배열/목록이 위치하는 경로");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "processor_type",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "데이터를 파싱 및 가공할 처리기(Processor) 유형");

            migrationBuilder.AlterColumn<string>(
                name: "label_field",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "화면에 표시할 텍스트에 해당하는 필드명");

            migrationBuilder.AlterColumn<string>(
                name: "http_method",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "HTTP 메서드 (예: GET, POST 등, 기본값: GET)");

            migrationBuilder.AlterColumn<string>(
                name: "biz_type",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "비즈니스 유형 (예: 계정구분, 권한그룹 등 특정 콤보박스가 사용할 도메인 유형)");

            migrationBuilder.AlterColumn<string>(
                name: "api_url",
                schema: "scom",
                table: "biz_select_configs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "데이터를 조회할 API 주소");

            migrationBuilder.AlterColumn<string>(
                name: "real_name",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "사용자 실명 : 반드시 2글자 이상 입력되어야 함.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "사용자 실명 (반드시 2글자 이상 입력되어야 함)");

            migrationBuilder.AlterColumn<string>(
                name: "department_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "소속 부서 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "소속 부서 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "소속 회사 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "소속 회사 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "avatar_group_id",
                schema: "scom",
                table: "accounts",
                type: "text",
                nullable: true,
                comment: "아바타 이미지 파일 그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "아바타 이미지 파일 그룹 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "account_id",
                schema: "scom",
                table: "account_profile_details",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "연관된 사용자 계정 식별자 (ID)");
        }
    }
}
