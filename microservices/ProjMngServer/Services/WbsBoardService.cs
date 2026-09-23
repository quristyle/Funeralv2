using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;
using static ProjMngServer.Services.WbsBoardSql;

namespace ProjMngServer.Services;

/// <summary>
/// WBS 대시보드의 집계와 상세 목록 — <c>projmng.wbs_work</c>.
/// </summary>
/// <remarks>
/// <para>
/// 사내망에서 따로 돌던 대시보드를 옮겨 온 것이다. 원본은 외부 패키지를 하나도
/// 쓸 수 없어(nuget 이 막혀 있었다) <b>PostgreSQL 프로토콜을 직접 구현한</b>
/// 드라이버로 조회했다(<c>Pg.cs</c> · <c>PgWriter.cs</c>, 442줄). 여기서는
/// 다른 서비스와 같이 Dapper 를 쓴다 — 그 두 파일은 옮기지 않았다.
/// </para>
///
/// <para>
/// [모든 조회가 프로젝트를 받는다]
/// </para>
///
/// <para>
/// 원본은 프로젝트 하나 전용이라 그런 조건이 없었다. 여기서는 <c>prjRid</c> 가
/// 조회마다 들어간다 — <b>기본값을 두지 않는다.</b> 빠뜨리면 컴파일이 막히는
/// 편이, 남의 프로젝트 숫자가 섞여 나오는 것보다 낫다.
/// </para>
/// </remarks>
public sealed class WbsBoardService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    // ──────────────────────────────────────────────────────── 요약

    public async Task<WbsBoardSummary> SummaryAsync(int prjRid, string? basis, string? scope)
    {
        var c = DateCol(basis);
        using var db = Open();

        return await db.QuerySingleAsync<WbsBoardSummary>($"""
            select count(*)::int                                          as Total
                 , count({c})::int                                        as Dated
                 , count(distinct systemcode)::int                        as Modules
                 , count(distinct user_bp_id)::int                        as Users
                 , min({c})::text                                         as MinDt
                 , max({c})::text                                         as MaxDt
                 , count(*) filter (where complate_yn = 'o')::int          as Done
                 , count(*) filter (where complate_real_yn = 'o')::int     as DoneReal
                 , count(*) filter (where complate_big_yn = 'o')::int      as DoneBig
                 , count(*) filter (where complate_real_big_yn = 'o')::int as DoneRealBig
              from projmng.wbs_work
             where prj_rid = @prjRid
               and {DevWhere(scope)}
            """, new { prjRid });
    }

    // ──────────────────────────────────────────────────────── 기간별

    public async Task<List<WbsBoardBucket>> MonthlyAsync(int prjRid, string? basis, string? scope)
    {
        var c = DateCol(basis);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardBucket>($"""
            select to_char(date_trunc('month', {c}), 'YYYY-MM')   as Bucket
                 , count(*)::int                                  as Cnt
                 , count(*) filter (where complate_yn = 'o')::int  as Done
                 , count(distinct systemcode)::int                 as Modules
                 , count(distinct user_bp_id)::int                 as Users
              from projmng.wbs_work
             where prj_rid = @prjRid
               and {DevWhere(scope)}
               and {c} is not null
             group by 1
             order by 1
            """, new { prjRid });

        return [.. rows];
    }

    /// <summary>주 단위. ISO 주라 월요일에 시작한다.</summary>
    public async Task<List<WbsBoardBucket>> WeeklyAsync(int prjRid, string? basis, string? scope)
    {
        var c = DateCol(basis);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardBucket>($"""
            select to_char(date_trunc('week', {c}), 'IYYY-"W"IW')            as Bucket
                 , date_trunc('week', {c})::date::text                       as WeekStart
                 -- 금요일까지만 보여 준다. 주말에 잡힌 일정이 거의 없어서
                 -- 일요일까지 그리면 막대 오른쪽이 늘 비어 보인다.
                 , (date_trunc('week', {c}) + interval '4 day')::date::text  as WeekEnd
                 , count(*)::int                                             as Cnt
                 , count(*) filter (where complate_yn = 'o')::int             as Done
                 , count(distinct user_bp_id)::int                            as Users
              from projmng.wbs_work
             where prj_rid = @prjRid
               and {DevWhere(scope)}
               and {c} is not null
             group by 1, 2, 3
             order by 2
            """, new { prjRid });

        return [.. rows];
    }

    // ──────────────────────────────────────────────────────── 사람 × 기간

    public async Task<List<WbsBoardUserBucket>> MonthlyByUserAsync(
        int prjRid, string? basis, string? scope, string? who)
    {
        var c = DateCol(basis);
        var (uc, dc, _) = WhoCols(who);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardUserBucket>($"""
            select coalesce(w.{uc}, '(미지정)')                                as UserBpId
                 , coalesce(u.name, w.{uc}, '(미지정)')                        as UserNm
                 , to_char(date_trunc('month', w.{c}), 'YYYY-MM')              as Bucket
                 , count(*)::int                                               as Cnt
                 , count(*) filter (where w.{dc} = 'o')::int                   as Done
                 -- 담당자 · 개발자 완료를 각각 따로도 준다. 요약 화면이 한 번의
                 -- 조회로 두 잣대를 나란히 보여 준다.
                 , count(*) filter (where w.complate_yn = 'o')::int             as DonePlan
                 , count(*) filter (where w.complate_real_yn = 'o')::int        as DoneReal
                 , count(*) filter (where w.complate_big_yn = 'o')::int         as DoneBig
                 , count(*) filter (where w.complate_real_big_yn = 'o')::int    as DoneRealBig
              from projmng.wbs_work w
              left join projmng.wbs_user u
                     on u.prj_rid = w.prj_rid and upper(u.bp_id) = upper(w.{uc})
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
               and w.{c} is not null
             group by 1, 2, 3
             order by 2, 1, 3
            """, new { prjRid });

        return [.. rows];
    }

    public async Task<List<WbsBoardUserBucket>> WeeklyByUserAsync(
        int prjRid, string? basis, string? scope, string? who)
    {
        var c = DateCol(basis);
        var (uc, dc, _) = WhoCols(who);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardUserBucket>($"""
            select coalesce(w.{uc}, '(미지정)')                                  as UserBpId
                 , coalesce(u.name, w.{uc}, '(미지정)')                          as UserNm
                 , to_char(date_trunc('week', w.{c}), 'IYYY-"W"IW')              as Bucket
                 , date_trunc('week', w.{c})::date::text                         as WeekStart
                 , (date_trunc('week', w.{c}) + interval '4 day')::date::text    as WeekEnd
                 , count(*)::int                                                 as Cnt
                 , count(*) filter (where w.{dc} = 'o')::int                     as Done
              from projmng.wbs_work w
              left join projmng.wbs_user u
                     on u.prj_rid = w.prj_rid and upper(u.bp_id) = upper(w.{uc})
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
               and w.{c} is not null
             group by 1, 2, 3, 4, 5
             order by 4, 2, 1
            """, new { prjRid });

        return [.. rows];
    }

    // ──────────────────────────────────────────────────────── 모듈별

    public async Task<List<WbsBoardModule>> ByModuleAsync(int prjRid, string? basis, string? scope)
    {
        var c = DateCol(basis);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardModule>($"""
            select systemcode                                              as Systemcode
                 , min(system_nm)                                          as SystemNm
                 , count(*)::int                                           as Cnt
                 , count(*) filter (where complate_yn = 'o')::int           as Done
                 , count(*) filter (where complate_real_yn = 'o')::int      as DoneReal
                 , count(*) filter (where complate_big_yn = 'o')::int       as DoneBig
                 , count(*) filter (where complate_real_big_yn = 'o')::int  as DoneRealBig
                 , count({c})::int                                         as Dated
                 , min({c})::text                                          as MinDt
                 , max({c})::text                                          as MaxDt
                 , count(plan_sdt_c)::int                                  as ActSdt
                 , count(plan_edt_c)::int                                  as ActEdt
                 , min(plan_sdt_c)::text                                   as MinAct
                 , max(plan_edt_c)::text                                   as MaxAct
              from projmng.wbs_work
             where prj_rid = @prjRid
               and {DevWhere(scope)}
             group by systemcode
             order by systemcode
            """, new { prjRid });

        return [.. rows];
    }

    // ──────────────────────────────────────────────────────── 담당자 고르개

    /// <summary>
    /// 담당자 목록. 기간을 주면 <b>그 기간에 실제로 배정된 사람만</b> 나온다 —
    /// 고르개에 빈 결과만 내는 항목이 섞이지 않는다.
    /// </summary>
    public async Task<List<WbsBoardUserOption>> UsersAsync(
        int prjRid, string? scope, string? basis, string? month, string? week)
    {
        var c = DateCol(basis);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardUserOption>($"""
            select coalesce(w.user_bp_id, '(미지정)')             as UserBpId
                 , coalesce(u.name
                          , case when w.user_bp_id is null then '미지정'
                                 else '(미등록)' end)             as UserNm
                 , count(*)::int                                  as Cnt
              from projmng.wbs_work w
              left join projmng.wbs_user u
                     on u.prj_rid = w.prj_rid and upper(u.bp_id) = upper(w.user_bp_id)
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
               and (@month::text is null
                    or to_char(date_trunc('month', w.{c}), 'YYYY-MM') = @month)
               and (@week::text is null
                    or to_char(date_trunc('week', w.{c}), 'IYYY-"W"IW') = @week)
             group by 1, 2
             order by 2, 1
            """, new { prjRid, month = Nz(month), week = Nz(week) });

        return [.. rows];
    }

    // ──────────────────────────────────────────────────────── 상세 목록

    /// <summary>
    /// 드릴다운 목록. 조건이 열둘이라 <see cref="WbsBoardQuery"/> 로 받는다.
    /// </summary>
    /// <remarks>
    /// 지연·진행 상태 조건은 [지연 현황]·[진척률 현황] 화면과 <b>같은 식</b>을
    /// 쓴다(<see cref="WbsBoardSql"/>). 그래서 차트에서 눌러 넘어온 목록의
    /// 건수가 차트의 숫자와 어긋나지 않는다.
    /// </remarks>
    public async Task<List<WbsBoardRow>> RowsAsync(int prjRid, WbsBoardQuery query)
    {
        var c = DateCol(query.Basis);

        var doneWhere = query.Done?.ToLowerInvariant() switch
        {
            "y" => " and w.complate_yn = 'o'",
            "n" => " and (w.complate_yn is distinct from 'o')",
            _ => "",
        };

        var lateWhere = query.Late?.ToLowerInvariant() switch
        {
            "start" => $" and {StartLate}",
            "finish" => $" and {FinishLate}",
            "both" => $" and {StartLate} and {FinishLate}",
            "any" => $" and ({StartLate} or {FinishLate})",
            "none" => $" and not ({StartLate} or {FinishLate})",
            _ => "",
        };

        // 착수도래 = 계획진척률이 0 보다 크다. 일정이 없어 진척률이 null 인 줄은
        // 「미도래」로 본다 — 진척률 화면이 null 을 0 으로 읽는 것과 같은 규칙이다.
        var started = $"coalesce((({PlanRate}) > 0), false)";
        var statusWhere = query.Status?.ToLowerInvariant() switch
        {
            "open" => $" and {started} and {DoneFlag} is not true",
            "notyet" => $" and not {started}",
            "done" => $" and {DoneFlag}",
            _ => "",
        };

        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardRow>($"""
            select w.activity_id           as ActivityId
                 , w.systemcode            as Systemcode
                 , w.system_nm             as SystemNm
                 , w.menu_nm               as MenuNm
                 , w.program_id            as ProgramId
                 , w.plan_sdt::text        as PlanSdt
                 , w.plan_edt::text        as PlanEdt
                 , w.plan_sdt_c::text      as PlanSdtC
                 , w.plan_edt_c::text      as PlanEdtC
                 , w.user_bp_id            as UserBpId
                 , u.name                  as UserNm
                 , w.complate_yn           as ComplateYn
                 , w.user_real_id          as UserRealId
                 , ru.name                 as UserRealNm
                 , w.complate_real_yn      as ComplateRealYn
                 , w.recheck_yn            as RecheckYn
                 , w.complate_big_yn       as ComplateBigYn
                 , w.complate_real_big_yn  as ComplateRealBigYn
                 , w.recheck_big_yn        as RecheckBigYn
                 , w.db_ready_big_yn       as DbReadyBigYn
                 , w.priority_order        as PriorityOrder
                 , w.prog_type             as ProgType
                 , w.prog_type_desc        as ProgTypeDesc
                 , w.trg_use_chk           as TrgUseChk
                 , {PlanRate}              as PlanRate
                 , {StartLate}             as StartLate
                 , {FinishLate}            as FinishLate
                 , case when {StartLate}  then current_date - w.plan_sdt end as StartDays
                 , case when {FinishLate} then current_date - w.plan_edt end as FinishDays
                 , p.pv_finish_rate        as PvFinishRate
                 , p.pv_actual_sdt::text   as PvActualSdt
                 , p.pv_actual_edt::text   as PvActualEdt
                 , p.pv_snapshot_at::text  as PvSnapshotAt
                 , pt.task_cnt::int        as PvTaskCnt
                 , pt.node_cnt::int        as PvNodeCnt
                 , pt.node_empty::int      as PvNodeEmpty
                 , pt.task_edt::text       as PvTaskEdt
                 , pt.workers              as PvWorkers
                 , pt.status               as PvStatus
                 , pt.status_at::text      as PvStatusAt
                 , pt.status_cnt::int      as PvStatusCnt
                 , pt.task_code            as PvTaskCode
              from projmng.wbs_work w
              left join projmng.wbs_user u
                     on u.prj_rid = w.prj_rid and upper(u.bp_id) = upper(w.user_bp_id)
              left join projmng.wbs_user ru
                     on ru.prj_rid = w.prj_rid and upper(ru.bp_id) = upper(w.user_real_id)
              left join projmng.wbs_pv p
                     on p.prj_rid = w.prj_rid and p.activity_id = w.activity_id
              left join {TaskRollup} pt
                     on pt.prj_rid = w.prj_rid and pt.activity_id = w.activity_id
             where w.prj_rid = @prjRid
               and {DevWhere(query.Scope, "w")}
               and (@month::text is null
                    or to_char(date_trunc('month', w.{c}), 'YYYY-MM') = @month)
               and (@week::text is null
                    or to_char(date_trunc('week', w.{c}), 'IYYY-"W"IW') = @week)
               and (@user::text is null or coalesce(w.user_bp_id, '(미지정)') = @user)
               and (@module::text is null or w.systemcode = @module)
               and (@q::text is null
                    or w.menu_nm ilike '%' || @q || '%'
                    or w.activity_id ilike '%' || @q || '%')
               and (@realUser::text is null or coalesce(w.user_real_id, '(미지정)') = @realUser)
               {doneWhere}{lateWhere}{statusWhere}
             order by w.{c} nulls last, w.systemcode, w.menu_nm
             limit @limit
            """, new
        {
            prjRid,
            month = Nz(query.Month),
            week = Nz(query.Week),
            user = Nz(query.User),
            module = Nz(query.Module),
            q = Nz(query.Q),
            realUser = Nz(query.RealUser),
            limit = Math.Clamp(query.Limit ?? 500, 1, 5000),
        });

        return [.. rows];
    }

    /// <summary>
    /// 단건 수정. <see cref="WbsBoardSql.Editable"/> 에 있는 칸만 받는다.
    /// </summary>
    /// <returns>고친 줄 수. 받을 칸이 하나도 없으면 <c>-1</c>.</returns>
    public async Task<int> PatchRowAsync(int prjRid, string activityId, IDictionary<string, object?> patch)
    {
        var sets = new List<string>();
        var args = new DynamicParameters();
        var i = 0;

        foreach (var (key, raw) in patch)
        {
            if (!Editable.Contains(key)) continue;

            // 빈 글자는 「지운다」로 읽는다. 원장의 표시 칸들이 'o' 아니면
            // 비어 있는 식이라, 빈 글자를 그대로 넣으면 '아무것도 아닌 값'이
            // 두 가지가 된다.
            var val = raw is string s && string.IsNullOrWhiteSpace(s) ? null : raw;

            var name = $"p{i++}";
            args.Add(name, val);
            sets.Add($"{key.ToLowerInvariant()} = @{name}{Cast(key)}");
        }

        if (sets.Count == 0) return -1;

        args.Add("prjRid", prjRid);
        args.Add("activityId", activityId);

        using var db = Open();

        return await db.ExecuteAsync($"""
            update projmng.wbs_work
               set {string.Join(", ", sets)}
             where prj_rid = @prjRid and activity_id = @activityId
            """, args);
    }
}
