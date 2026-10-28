namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>상세 목록의 칸 하나. 묶음(Group)은 칸 고르기 창이 나눠 보여 주는 단위다.</summary>
public sealed record WbsRowCol(string Key, string Label, string Group);

/// <summary>대상 추가·수정 창의 입력칸 하나. Kind — text · date · check · area · user · prio.</summary>
public sealed record WbsRowField(
    string Key, string Label, string Group, string Kind = "text", bool Required = false, bool Wide = false);

/// <summary>상세 목록의 칸과 추가·수정 창의 입력칸 정의. 참고 소스(store.js COLS · RowFormDialog FIELDS)와 같은 차례다.</summary>
public static class WbsRowSchema
{
    public static readonly IReadOnlyList<WbsRowCol> Cols =
    [
        new("activity_id", "Activity ID", "기본"),
        new("systemcode", "모듈", "기본"),
        new("menu_nm", "메뉴명", "기본"),
        new("plan_sdt", "시작일", "일정"),
        new("plan_edt", "종료일", "일정"),
        new("plan_sdt_c", "실적시작", "일정"),
        new("plan_edt_c", "실적종료", "일정"),
        new("plan_rate", "계획진척률", "일정"),
        new("user_bp_id", "담당자", "담당"),
        new("complate_yn", "완료", "담당"),
        new("user_real_id", "개발자", "담당"),
        new("complate_real_yn", "개발자완료", "담당"),
        new("recheck_yn", "재확인요청", "담당"),
        new("complate_big_yn", "대형완료", "대형"),
        new("complate_real_big_yn", "대형개발자완료", "대형"),
        new("recheck_big_yn", "대형재확인요청", "대형"),
        new("db_ready_big_yn", "대형DB준비여부", "대형"),
        new("priority_order", "우선순위", "속성"),
        new("prog_type", "화면유형", "속성"),
        new("trg_use_chk", "트리거 발화", "속성"),
        new("dwg_view_use", "도면조회", "속성"),
        new("report_use", "리포트", "속성"),
        new("task_cnt", "일감", "속성"),
        new("file_cnt", "첨부", "속성"),
        new("pv_status", "PV 현재단계", "ProjectView"),
        new("pv_finish_rate", "PV 진척률", "ProjectView"),
        new("pv_actual_sdt", "PV 실적시작", "ProjectView"),
        new("pv_task_cnt", "PV 일감", "ProjectView"),
        new("pv_task_edt", "PV 일감완료일", "ProjectView"),
        new("pv_workers", "PV 일감담당자", "ProjectView"),
        new("pv_node_empty", "PV 빈단계", "ProjectView"),
        new("pv_snapshot_at", "PV 수집", "ProjectView"),
    ];

    /// <summary>전부 끄고 되돌릴 때 남기는 최소 구성.</summary>
    public static readonly IReadOnlyList<string> BaseCols =
        ["activity_id", "systemcode", "menu_nm", "plan_edt", "user_bp_id", "complate_yn"];

    public static readonly IReadOnlyList<WbsRowField> Fields =
    [
        new("activity_id", "Activity ID", "기본"),
        new("pv_test_case_id", "단위테스트 코드", "기본"),
        new("systemcode", "모듈 코드", "기본", Required: true),
        new("system_nm", "모듈명", "기본"),
        new("menu_nm", "메뉴명", "기본", Required: true, Wide: true),
        new("program_id", "프로그램 ID", "기본"),
        new("priority_order", "우선순위", "기본", "prio"),
        new("module", "업무 대분류", "기본"),
        new("module_name", "업무 대분류명", "기본"),
        new("new_dev2", "실작업대상", "기본", "check"),

        new("plan_sdt", "계획시작일", "일정", "date"),
        new("plan_edt", "계획종료일", "일정", "date"),
        new("plan_sdt_c", "실적시작일", "일정", "date"),
        new("plan_edt_c", "실적종료일", "일정", "date"),

        new("user_bp_id", "담당자", "담당", "user"),
        new("user_real_id", "개발자", "담당", "user"),
        new("complate_yn", "완료", "담당", "check"),
        new("complate_real_yn", "개발자완료", "담당", "check"),
        new("recheck_yn", "재확인요청", "담당", "check"),
        new("comp_desc", "완료 코멘트", "담당", "area", Wide: true),

        new("complate_big_yn", "대형완료", "대형", "check"),
        new("complate_real_big_yn", "대형개발자완료", "대형", "check"),
        new("recheck_big_yn", "대형재확인요청", "대형", "check"),
        new("db_ready_big_yn", "대형DB준비여부", "대형", "check"),

        new("prog_type", "화면유형", "화면 속성"),
        new("asis_cs", "AS-IS C# 소스", "화면 속성"),
        new("prog_type_desc", "화면 기능 요약", "화면 속성", "area", Wide: true),
        new("menu_desc", "화면 설명", "화면 속성", "area", Wide: true),
        new("report_use", "리포트 사용", "화면 속성", "check"),
        new("sheet_use", "엑셀 사용", "화면 속성", "check"),
        new("dwg_view_use", "도면 조회 사용", "화면 속성", "check"),
        new("por_view_use", "POR 조회 사용", "화면 속성", "check"),
        new("mon_req_comp", "월 완료 요청본", "화면 속성", "check"),

        new("trg_exsit_chk", "트리거 존재 확인", "트리거"),
        new("trg_use_chk", "발화 트리거", "트리거", Wide: true),
        new("trg_evt_rel", "연계 이벤트", "트리거", "area", Wide: true),

        new("comment", "개발 준비 메모", "메모", "area", Wide: true),
        new("etc_desc", "기타 특이사항", "메모", "area", Wide: true),
    ];

    /// <summary>일감 구분. 원장에 쌓인 값(개발·확인·문의·버그·기타·정정)과 같다.</summary>
    public static readonly IReadOnlyList<string> TaskDivs = ["개발", "확인", "문의", "버그", "기타", "정정"];
}
