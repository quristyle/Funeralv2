using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// WBS 대시보드의 조회들이 나눠 쓰는 SQL 조각과 화이트리스트.
/// </summary>
/// <remarks>
/// <para>
/// 여기 있는 것은 전부 <b>식별자이거나 식</b>이다 — 값이 아니다. PostgreSQL 은
/// 칸 이름을 매개변수로 바인딩할 수 없으므로 화이트리스트를 거쳐 문자열로
/// 끼워 넣는다. <b>사용자가 준 글자가 이 파일 밖에서 SQL 로 들어가는 길은
/// 없다</b> — 모르는 값은 전부 기본값으로 떨어진다.
/// </para>
///
/// <para>
/// 다섯 조회(요약 · 진척률 · 지연 · 상세 목록 · 모듈)가 같은 식을 쓴다.
/// 갈라 두면 <b>같은 화면끼리 숫자가 어긋난다</b> — 지연 판정을 한 곳만 고치면
/// [지연 현황]의 건수와 [상세 목록]의 지연 필터가 다른 줄을 집는다.
/// </para>
/// </remarks>
internal static class WbsBoardSql
{
    /// <summary>
    /// 집계 기준 날짜 칸. 기본은 계획종료일이다 — 대시보드가 「언제까지」를
    /// 묻는 물건이라 그렇다.
    /// </summary>
    public static string DateCol(string? basis) => basis?.ToLowerInvariant() switch
    {
        "sdt" or "plan_sdt" => "plan_sdt",
        _ => "plan_edt",
    };

    /// <summary>
    /// 조회 범위. 기본은 개발 대상만(<c>new_dev2 = 'o'</c>), <c>all</c> 이면 전부.
    /// </summary>
    /// <param name="alias">원장 표의 별칭. 없으면 칸 이름만 쓴다.</param>
    public static string DevWhere(string? scope, string? alias = null)
    {
        if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)) return "true";
        var p = string.IsNullOrEmpty(alias) ? "" : alias + ".";
        return $"{p}new_dev2 = 'o'";
    }

    /// <summary>
    /// 집계 기준 인물. <c>real</c> 이면 개발자, 그 밖이면 담당자.
    /// </summary>
    /// <returns>(사람 칸, 완료 칸, 대형 완료 칸)</returns>
    public static (string User, string Done, string DoneBig) WhoCols(string? who) =>
        string.Equals(who, "real", StringComparison.OrdinalIgnoreCase)
            ? ("user_real_id", "complate_real_yn", "complate_real_big_yn")
            : ("user_bp_id", "complate_yn", "complate_big_yn");

    /// <summary>
    /// 계획 진척률. 계획시작일 전이면 0, 계획종료일을 지났으면 100, 그 사이는 비율.
    /// </summary>
    /// <remarks>
    /// 하루짜리 작업(시작일 = 종료일)은 도래하는 순간 100 이 된다 — 분모가 0 이라
    /// 나눗셈에 못 들어가고, 그 날 안에 끝내야 하는 일이라 뜻도 맞는다.
    /// </remarks>
    public const string PlanRate = """
        case when w.plan_sdt is null or w.plan_edt is null then null
             when current_date <  w.plan_sdt then 0
             when current_date >= w.plan_edt then 100
             else round((current_date - w.plan_sdt)::numeric / (w.plan_edt - w.plan_sdt) * 100, 1)
        end
        """;

    /// <summary>담당자 완료.</summary>
    public const string DoneFlag = "(w.complate_yn = 'o')";

    /// <summary>대형 완료. <b>위와 따로 센다</b> — 완료율에 섞지 않는다.</summary>
    public const string DoneBigFlag = "(w.complate_big_yn = 'o')";

    /// <summary>착수지연 — 계획시작일이 도래했는데 실적시작일이 비어 있다.</summary>
    public const string StartLate =
        "(w.plan_sdt is not null and w.plan_sdt <= current_date and w.plan_sdt_c is null)";

    /// <summary>종료지연 — 계획종료일이 도래했는데 실적종료일이 비어 있다.</summary>
    public const string FinishLate =
        "(w.plan_edt is not null and w.plan_edt <= current_date and w.plan_edt_c is null)";

    /// <summary>
    /// 액티비티별 ProjectView 일감 집계. 상세 목록이 원장에 이것을 붙여 읽는다.
    /// </summary>
    /// <remarks>
    /// 한 액티비티에 일감이 여럿이면 <b>가장 나중에 바뀐 것</b>을 현재단계로 삼는다.
    /// 여럿이 섞여 있다는 사실 자체는 <c>status_cnt</c> 로 함께 내려보낸다 —
    /// 대표 하나만 보이면 「왜 저 단계지」를 뒤지게 된다.
    /// </remarks>
    public const string TaskRollup = """
        (select t.prj_rid
              , t.activity_id
              , count(*)                          as task_cnt
              , sum(coalesce(t.pv_node_cnt, 0))   as node_cnt
              , sum(coalesce(t.pv_node_empty, 0)) as node_empty
              , max(t.pv_plan_edt)                as task_edt
              , (array_agg(t.pv_status order by t.pv_status_at desc nulls last)
                   filter (where t.pv_status is not null))[1] as status
              , (array_agg(t.pv_task_code order by t.pv_status_at desc nulls last)
                   filter (where t.pv_task_code is not null))[1] as task_code
              , max(t.pv_status_at)               as status_at
              , count(distinct t.pv_status)       as status_cnt
              , (select string_agg(distinct n.worker_nm, ', ')
                   from projmng.wbs_pv_node n
                   join projmng.wbs_pv_task t2
                     on t2.prj_rid = n.prj_rid and t2.pv_task_id = n.pv_task_id
                  where t2.prj_rid = t.prj_rid
                    and t2.activity_id = t.activity_id
                    and n.worker_nm is not null) as workers
           from projmng.wbs_pv_task t
          group by t.prj_rid, t.activity_id)
        """;

    /// <summary>
    /// 상세 목록에서 고칠 수 있는 칸.
    /// </summary>
    /// <remarks>
    /// 일정(<c>plan_sdt</c>·<c>plan_edt</c>)과 실적(<c>plan_sdt_c</c>·
    /// <c>plan_edt_c</c>)은 <b>엑셀 WBS 가 원본</b>이라 들어 있지 않다. 여기서
    /// 고치면 다음 동기화에 조용히 덮인다 — 고칠 수 없는 편이 낫다.
    /// </remarks>
    public static readonly HashSet<string> Editable = new(StringComparer.OrdinalIgnoreCase)
    {
        "systemcode", "system_nm", "menu_nm",
        "user_bp_id", "priority_order", "prog_type", "prog_type_desc",
        "etc_desc", "comment", "new_dev2", "report_use", "complate_yn",
        "user_real_id", "complate_real_yn", "recheck_yn",
        "complate_big_yn", "complate_real_big_yn", "recheck_big_yn",
        "db_ready_big_yn",
    };

    /// <summary>개발자 명부에서 고칠 수 있는 칸. 열쇠(<c>prj_rid</c>·<c>bp_id</c>)는 뺀다.</summary>
    public static readonly string[] UserCols =
    [
        "login_id",
        "name", "position_nm", "birth_dt", "email", "tel_no", "emerg_tel_no", "use_ip",
        "mac_addr", "notebook_no", "notebook_chk_no",
        "super_yn", "block_yn",
        "monitor1_no", "monitor1_chk_no",
        "monitor2_no", "monitor2_chk_no",
        "monitor3_no", "monitor3_chk_no",
        "summer_size", "winter_size",
        "git", "startkit", "dxb", "vm_conn", "aipro", "claudecode",
        "dev_db", "wiki", "projectview", "svn", "notebook", "hub_hdmi",
        "pv_user_id",
    ];

    /// <inheritdoc cref="UserCols"/>
    public static readonly HashSet<string> UserColSet = new(UserCols, StringComparer.OrdinalIgnoreCase);

    /// <summary>날짜로 넣어야 하는 칸. 나머지는 글자로 넣는다.</summary>
    public static readonly HashSet<string> DateCols = new(StringComparer.OrdinalIgnoreCase)
    {
        "birth_dt",
    };

    /// <summary>그 칸에 맞는 형변환 꼬리표.</summary>
    public static string Cast(string col) => DateCols.Contains(col) ? "::date" : "::text";

    /// <summary>빈 글자는 <c>null</c> 로 본다. 조건이 「안 걸린다」는 뜻이 되어야 한다.</summary>
    public static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>
    /// 본문에서 온 값 하나를 DB 에 넣을 수 있는 것으로 바꾼다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 한 줄이 없으면 수정이 통째로 500 이 난다</b>(실제로 밟음).
    /// <c>Dictionary&lt;string, object?&gt;</c> 로 본문을 받으면 값이 CLR 타입이
    /// 아니라 <see cref="JsonElement"/> 로 들어오고, Dapper 는 그것을 매개변수로
    /// 받지 못한다 — <c>The member p0 of type System.Text.Json.JsonElement
    /// cannot be used as a parameter value</c>.
    /// </para>
    ///
    /// <para>
    /// 조용히 틀리지 않고 <b>터지는</b> 쪽이라 발견은 빨랐지만, 화면이 「고치기」를
    /// 누르는 순간이라 눈에 띄는 자리가 좋지는 않다.
    /// </para>
    /// </remarks>
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
