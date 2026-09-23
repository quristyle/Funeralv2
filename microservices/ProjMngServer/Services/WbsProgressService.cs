using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;
using static ProjMngServer.Services.WbsBoardSql;

namespace ProjMngServer.Services;

/// <summary>
/// 계획 진척률 — 「오늘 기준으로 얼마나 지났어야 하는가」.
/// </summary>
/// <remarks>
/// <para>
/// 실적이 아니다. 계획시작일과 계획종료일만 보고 <b>날짜가 얼마나 흘렀는지</b>를
/// 센다(<see cref="WbsBoardSql.PlanRate"/>). 그 옆에 실제 완료율을 나란히 두는
/// 것이 이 화면의 전부다 — 둘이 벌어진 만큼이 늦은 것이다.
/// </para>
///
/// <para>
/// 기준일은 <b>DB 의 <c>current_date</c></b> 다. 브라우저 시각을 쓰면 시차가
/// 다른 자리에서 같은 화면이 다른 숫자를 낸다.
/// </para>
/// </remarks>
public sealed class WbsProgressService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    public async Task<WbsBoardProgress> SummaryAsync(int prjRid, string? scope)
    {
        using var db = Open();

        return await db.QuerySingleAsync<WbsBoardProgress>($"""
            with t as (
              select w.*
                   , {PlanRate}    as rate
                   , {DoneFlag}    as done
                   , {DoneBigFlag} as done_big
                from projmng.wbs_work w
               where w.prj_rid = @prjRid
                 and {DevWhere(scope, "w")}
            )
            select current_date::text                                     as Asof
                 , count(*)::int                                          as Total
                 , count(rate)::int                                       as Dated
                 , round(avg(rate), 1)                                    as PlanRate
                 , count(*) filter (where rate = 0)::int                   as NotStarted
                 , count(*) filter (where rate > 0 and rate < 100)::int     as InProgress
                 , count(*) filter (where rate = 100)::int                  as Elapsed
                 , count(*) filter (where done)::int                        as DoneCnt
                 , round(count(*) filter (where done)::numeric
                         / nullif(count(*), 0) * 100, 1)                   as DoneRate
                 , count(*) filter (where done_big)::int                    as DoneBigCnt
                 , round(count(*) filter (where done_big)::numeric
                         / nullif(count(*), 0) * 100, 1)                   as DoneBigRate
              from t
            """, new { prjRid });
    }

    public async Task<List<WbsBoardProgressUser>> ByUserAsync(int prjRid, string? scope, string? who)
    {
        var (uc, dc, bc) = WhoCols(who);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardProgressUser>($"""
            select coalesce(w.{uc}, '(미지정)')                                    as UserBpId
                 , count(*)::int                                                   as Cnt
                 , round(avg({PlanRate}), 1)                                       as PlanRate
                 , count(*) filter (where ({PlanRate}) = 0)::int                    as NotStarted
                 , count(*) filter (where ({PlanRate}) > 0
                                      and ({PlanRate}) < 100)::int                  as InProgress
                 , count(*) filter (where ({PlanRate}) = 100)::int                  as Elapsed
                 , count(*) filter (where w.{dc} = 'o')::int                        as DoneCnt
                 , round(count(*) filter (where w.{dc} = 'o')::numeric
                         / nullif(count(*), 0) * 100, 1)                           as DoneRate
                 , count(*) filter (where w.{bc} = 'o')::int                        as DoneBigCnt
                 , round(count(*) filter (where w.{bc} = 'o')::numeric
                         / nullif(count(*), 0) * 100, 1)                           as DoneBigRate
              from projmng.wbs_work w
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
             group by 1
             order by 3 desc nulls last, 1
            """, new { prjRid });

        return [.. rows];
    }

    public async Task<List<WbsBoardProgressModule>> ByModuleAsync(int prjRid, string? scope)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardProgressModule>($"""
            select w.systemcode                                        as Systemcode
                 , min(w.system_nm)                                    as SystemNm
                 , count(*)::int                                       as Cnt
                 , round(avg({PlanRate}), 1)                           as PlanRate
                 , count(*) filter (where {DoneFlag})::int              as DoneCnt
                 , round(count(*) filter (where {DoneFlag})::numeric
                         / nullif(count(*), 0) * 100, 1)               as DoneRate
                 , count(*) filter (where {DoneBigFlag})::int           as DoneBigCnt
                 , round(count(*) filter (where {DoneBigFlag})::numeric
                         / nullif(count(*), 0) * 100, 1)               as DoneBigRate
                 , min(w.plan_sdt)::text                               as MinDt
                 , max(w.plan_edt)::text                               as MaxDt
              from projmng.wbs_work w
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
             group by 1
             order by 1
            """, new { prjRid });

        return [.. rows];
    }

    /// <summary>
    /// 항목별 상세. 진척률과 함께 <b>기간·경과 일수</b>를 같이 준다 —
    /// 분모와 분자를 화면에서 다시 세면 반올림 탓에 숫자가 어긋난다.
    /// </summary>
    public async Task<List<WbsBoardProgressRow>> RowsAsync(
        int prjRid, string? scope, string? user, string? realUser, string? module)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardProgressRow>($"""
            select w.activity_id      as ActivityId
                 , w.systemcode       as Systemcode
                 , w.system_nm        as SystemNm
                 , w.menu_nm          as MenuNm
                 , w.plan_sdt::text   as PlanSdt
                 , w.plan_edt::text   as PlanEdt
                 , w.plan_sdt_c::text as PlanSdtC
                 , w.plan_edt_c::text as PlanEdtC
                 , (w.plan_edt - w.plan_sdt)                                       as SpanDays
                 , greatest(0, least(current_date - w.plan_sdt,
                                     w.plan_edt - w.plan_sdt))                     as PassedDays
                 , {PlanRate}         as PlanRate
                 , w.complate_yn      as ComplateYn
                 , w.user_bp_id       as UserBpId
                 , w.user_real_id     as UserRealId
                 , w.complate_real_yn as ComplateRealYn
                 , w.priority_order   as PriorityOrder
              from projmng.wbs_work w
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
               and (@user::text is null or coalesce(w.user_bp_id, '(미지정)') = @user)
               and (@realUser::text is null or coalesce(w.user_real_id, '(미지정)') = @realUser)
               and (@module::text is null or w.systemcode = @module)
             order by ({PlanRate}) desc nulls last, w.plan_edt, w.systemcode, w.menu_nm
            """, new
        {
            prjRid,
            user = Nz(user),
            realUser = Nz(realUser),
            module = Nz(module),
        });

        return [.. rows];
    }
}
