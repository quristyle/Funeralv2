using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;
using static ProjMngServer.Services.WbsBoardSql;

namespace ProjMngServer.Services;

/// <summary>
/// 지연 현황 — 「도래했는데 실적이 비어 있다」.
/// </summary>
/// <remarks>
/// <para>
/// 착수지연과 종료지연은 서로 독립이다. 한 줄이 둘 다일 수 있어서 합계를
/// 더하면 겹쳐 세어진다 — 그래서 <c>BothLate</c>·<c>AnyLate</c> 를 따로 준다.
/// </para>
///
/// <para>
/// 판정식은 <see cref="WbsBoardSql.StartLate"/>·<see cref="WbsBoardSql.FinishLate"/>
/// 한 벌이고 [상세 목록]의 지연 필터도 그것을 쓴다. <b>갈라 두면 차트에서
/// 눌러 넘어간 목록의 건수가 차트와 달라진다.</b>
/// </para>
/// </remarks>
public sealed class WbsDelayService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    public async Task<WbsBoardDelay> SummaryAsync(int prjRid, string? scope)
    {
        using var db = Open();

        return await db.QuerySingleAsync<WbsBoardDelay>($"""
            select current_date::text                                           as Asof
                 , count(*)::int                                                as Total
                 , count(*) filter (where {StartLate})::int                      as StartLate
                 , count(*) filter (where {FinishLate})::int                     as FinishLate
                 , count(*) filter (where {StartLate} and {FinishLate})::int      as BothLate
                 , count(*) filter (where {StartLate} or {FinishLate})::int       as AnyLate
                 , max(current_date - w.plan_sdt) filter (where {StartLate})      as MaxStartDays
                 , max(current_date - w.plan_edt) filter (where {FinishLate})     as MaxFinishDays
              from projmng.wbs_work w
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
            """, new { prjRid });
    }

    public async Task<List<WbsBoardDelayUser>> ByUserAsync(int prjRid, string? scope, string? who)
    {
        var (uc, _, _) = WhoCols(who);
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardDelayUser>($"""
            select coalesce(w.{uc}, '(미지정)')                                as UserBpId
                 , count(*)::int                                               as Assigned
                 , count(*) filter (where {StartLate})::int                     as StartLate
                 , count(*) filter (where {FinishLate})::int                    as FinishLate
                 , count(*) filter (where {StartLate} or {FinishLate})::int      as AnyLate
              from projmng.wbs_work w
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
             group by 1
             order by 5 desc, 1
            """, new { prjRid });

        return [.. rows];
    }

    /// <param name="kind"><c>start</c> · <c>finish</c> · 그 밖이면 둘 중 하나라도.</param>
    public async Task<List<WbsBoardDelayRow>> RowsAsync(
        int prjRid, string? scope, string? kind, string? user, string? realUser)
    {
        var where = kind?.ToLowerInvariant() switch
        {
            "start" => StartLate,
            "finish" => FinishLate,
            _ => $"({StartLate} or {FinishLate})",
        };

        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardDelayRow>($"""
            select w.activity_id      as ActivityId
                 , w.systemcode       as Systemcode
                 , w.system_nm        as SystemNm
                 , w.menu_nm          as MenuNm
                 , w.plan_sdt::text   as PlanSdt
                 , w.plan_edt::text   as PlanEdt
                 , w.plan_sdt_c::text as PlanSdtC
                 , w.plan_edt_c::text as PlanEdtC
                 , w.user_bp_id       as UserBpId
                 , w.user_real_id     as UserRealId
                 , w.complate_yn      as ComplateYn
                 , w.complate_real_yn as ComplateRealYn
                 , {StartLate}        as StartLate
                 , {FinishLate}       as FinishLate
                 , case when {StartLate}  then current_date - w.plan_sdt end as StartDays
                 , case when {FinishLate} then current_date - w.plan_edt end as FinishDays
                 , w.priority_order   as PriorityOrder
                 , w.prog_type        as ProgType
              from projmng.wbs_work w
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
               and {where}
               and (@user::text is null or coalesce(w.user_bp_id, '(미지정)') = @user)
               and (@realUser::text is null or coalesce(w.user_real_id, '(미지정)') = @realUser)
             -- 오래 밀린 것부터. 착수·종료 중 <더 밀린 쪽>으로 줄을 세운다.
             order by greatest(coalesce(current_date - w.plan_sdt, 0),
                               coalesce(current_date - w.plan_edt, 0)) desc
                    , w.systemcode, w.menu_nm
            """, new { prjRid, user = Nz(user), realUser = Nz(realUser) });

        return [.. rows];
    }
}
