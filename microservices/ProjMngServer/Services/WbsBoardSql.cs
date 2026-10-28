using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>WBS 대시보드의 조회들이 나눠 쓰는 SQL 조각과 화이트리스트. 식별자와 식만 담는다 — PostgreSQL 은 칸 이름을 매개변수로 바인딩할 수 없으므로 화이트리스트를 거쳐 문자열로 끼운다.</summary>
internal static class WbsBoardSql
{
    /// <summary>집계 기준 날짜 칸. 기본은 계획종료일이다 — 대시보드가 「언제까지」를 묻기 때문이다.</summary>
    public static string DateCol(string? basis) => basis?.ToLowerInvariant() switch
    {
        "sdt" or "plan_sdt" => "plan_sdt",
        _ => "plan_edt",
    };

    /// <summary>조회 범위. 기본은 개발 대상만(<c>new_dev2 = 'o'</c>), <c>all</c> 이면 전부.</summary>
    /// <param name="alias">원장 표의 별칭. 없으면 칸 이름만 쓴다.</param>
    public static string DevWhere(string? scope, string? alias = null)
    {
        if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)) return "true";
        var p = string.IsNullOrEmpty(alias) ? "" : alias + ".";
        return $"{p}new_dev2 = 'o'";
    }

    /// <summary>집계 기준 인물. <c>real</c> 이면 개발자, 그 밖이면 담당자.</summary>
    /// <returns>(사람 칸, 완료 칸, 대형 완료 칸)</returns>
    public static (string User, string Done, string DoneBig) WhoCols(string? who) =>
        string.Equals(who, "real", StringComparison.OrdinalIgnoreCase)
            ? ("user_real_id", "complate_real_yn", "complate_real_big_yn")
            : ("user_bp_id", "complate_yn", "complate_big_yn");

    /// <summary>계획 진척률. 계획시작일 전이면 0, 계획종료일을 지났으면 100, 그 사이는 비율. 하루짜리 작업은 도래하는 순간 100 이 된다 — 분모가 0 이라 나눗셈에 못 들어가고, 그 날 안에 끝내야 하는 일이라 뜻도 맞는다.</summary>
    public const string PlanRate = """
        case when w.plan_sdt is null or w.plan_edt is null then null
             when projmng.today_kst() <  w.plan_sdt then 0
             when projmng.today_kst() >= w.plan_edt then 100
             else round((projmng.today_kst() - w.plan_sdt)::numeric / (w.plan_edt - w.plan_sdt) * 100, 1)
        end
        """;

    /// <summary>담당자 완료.</summary>
    public const string DoneFlag = "(w.complate_yn = 'o')";

    /// <summary>대형 완료. <b>위와 따로 센다</b> — 완료율에 섞지 않는다.</summary>
    public const string DoneBigFlag = "(w.complate_big_yn = 'o')";

    /// <summary>착수지연 — 계획시작일이 도래했는데 실적시작일이 비어 있다.</summary>
    public const string StartLate =
        "(w.plan_sdt is not null and w.plan_sdt <= projmng.today_kst() and w.plan_sdt_c is null)";

    /// <summary>종료지연 — 계획종료일이 도래했는데 실적종료일이 비어 있다.</summary>
    public const string FinishLate =
        "(w.plan_edt is not null and w.plan_edt <= projmng.today_kst() and w.plan_edt_c is null)";

    /// <summary>상세 목록에서 고칠 수 있는 칸. 일정과 실적은 엑셀 WBS 가 원본이라 넣지 않는다 — 여기서 고치면 다음 동기화에 조용히 덮인다.</summary>
    public static readonly HashSet<string> Editable = new(StringComparer.OrdinalIgnoreCase)
    {
        "systemcode", "system_nm", "menu_nm",
        "user_bp_id", "priority_order", "prog_type", "prog_type_desc",
        "etc_desc", "comment", "new_dev2", "report_use", "complate_yn",
        "user_real_id", "complate_real_yn", "recheck_yn",
        "complate_big_yn", "complate_real_big_yn", "recheck_big_yn",
        "db_ready_big_yn", "dwg_view_use",
    };

    /// <summary>대상 추가·수정 창이 다루는 칸 전부. 키(activity_id)는 넣지 않는다 — 바꾸는 길은 「ID 지정」 하나다.</summary>
    public static readonly string[] FormColumns =
    [
        "pv_test_case_id", "systemcode", "system_nm", "menu_nm", "program_id", "priority_order",
        "module", "module_name", "new_dev2",
        "plan_sdt", "plan_edt", "plan_sdt_c", "plan_edt_c",
        "user_bp_id", "user_real_id", "complate_yn", "complate_real_yn", "recheck_yn", "comp_desc",
        "complate_big_yn", "complate_real_big_yn", "recheck_big_yn", "db_ready_big_yn",
        "prog_type", "asis_cs", "prog_type_desc", "menu_desc",
        "report_use", "sheet_use", "dwg_view_use", "por_view_use", "mon_req_comp",
        "trg_exsit_chk", "trg_use_chk", "trg_evt_rel",
        "comment", "etc_desc",
    ];

    /// <summary>날짜 칸. 나머지는 전부 글자다.</summary>
    public static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "plan_sdt", "plan_edt", "plan_sdt_c", "plan_edt_c",
    };

    /// <summary>Activity ID 가 아직 없는 줄의 임시 번호 앞머리. 원장의 키라 비워 둘 수 없어 이 번호로 세우고, 「ID 지정」이 확정 ID 로 바꾼다.</summary>
    public const string NoIdPrefix = "NOID-";

    /// <summary>ProjectView 일감 캐시를 액티비티 하나로 접는다. 일감이 여럿이면 가장 나중에 바뀐 단계를 대표로 삼는다.</summary>
    public const string PvTaskRollup = """
        (select t.activity_id
              , count(*)::int                          as task_cnt
              , sum(coalesce(t.pv_node_cnt, 0))::int   as node_cnt
              , sum(coalesce(t.pv_node_empty, 0))::int as node_empty
              , max(t.pv_plan_edt)                     as task_edt
              , (array_agg(t.pv_status order by t.pv_status_at desc nulls last)
                   filter (where t.pv_status is not null))[1] as status
              , (array_agg(t.pv_task_code order by t.pv_status_at desc nulls last)
                   filter (where t.pv_task_code is not null))[1] as task_code
              , max(t.pv_status_at)                    as status_at
              , count(distinct t.pv_status)::int       as status_cnt
              , (select string_agg(distinct n.worker_nm, ', ')
                   from projmng.wbs_pv_node n
                   join projmng.wbs_pv_task t2 on t2.prj_rid = n.prj_rid and t2.pv_task_id = n.pv_task_id
                  where t2.prj_rid = t.prj_rid and t2.activity_id = t.activity_id
                    and n.worker_nm is not null) as workers
           from projmng.wbs_pv_task t
          where t.prj_rid = @prjRid
          group by t.prj_rid, t.activity_id)
        """;

    /// <summary>빈 글자는 <c>null</c> 로 본다. 조건이 「안 걸린다」는 뜻이 되어야 한다.</summary>
    public static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>본문에서 온 값 하나를 DB 에 넣을 수 있는 것으로 바꾼다. 이 한 줄이 없으면 수정이 통째로 500 이 난다 — 본문을 <c>Dictionary&lt;string, object?&gt;</c> 로 받으면 값이 CLR 타입이라 그대로 넣을 수 없다.</summary>
    public static object? JsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => el.TryGetInt64(out var n) ? n : el.GetDouble(),
        JsonValueKind.String => el.GetString(),

        // 객체·배열은 이 표에 넣을 자리가 없다. 글자로 굳혀 두면 다음에
        // 「왜 {} 가 들어갔나」를 뒤지게 되므로 아예 비운다.
        _ => null,
    };

    /// <inheritdoc cref="JsonValue"/>
    public static Dictionary<string, object?> JsonValues(IDictionary<string, JsonElement> body)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, el) in body) map[key] = JsonValue(el);
        return map;
    }
}
