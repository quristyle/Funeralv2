using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 업무 비서(AI)가 쓰는 <b>내부 전용</b> 조회. 게이트웨이의 사람 인증을 거치지 않고,
/// 서버 간 머리글(<see cref="JSini.Shared.Internal.InternalCallGuard"/>)만 본다 — 평소 권한으로는
/// 못 보는 다른 프로젝트·다른 사람의 일감까지 비서가 대신 찾아 주기 위한 길이라, 프로젝트로도
/// 회사로도 거르지 않는다.
/// </summary>
public sealed class InternalQueryService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    /// <summary>
    /// 일정(<c>projmng.dev_wbs</c>) 한 줄. 개발·빌드·검수 세 사람과 세 확인 칸을 그대로 들고 와서
    /// 컨트롤러가 역할 매칭과 done 판정을 계산한다 — SQL 안에서 사람 비교까지 다 해 버리면
    /// WbsRoleMatch 가 쓸 bool 셋을 다시 못 얻는다.
    /// </summary>
    public sealed record WbsScheduleRow(
        string? ProcNm, DateOnly? PlanSdt, DateOnly? PlanEdt,
        string? DevUser, string? BuildUser, string? QcUser,
        string? DevChk, string? BuildChk, string? QcChk);

    /// <summary>
    /// 일정 목록의 전체 건수와 한 쪽(<paramref name="take"/>)을 함께 돌려준다.
    /// 기간은 겹침으로 본다(계획 종료일이 <paramref name="from"/> 뒤, 계획 시작일이
    /// <paramref name="to"/> 앞) — 일정은 하루짜리가 아니라 구간이라, 시작일만 보면
    /// 그 주에 걸쳐 있는 긴 일감이 빠진다.
    /// </summary>
    public async Task<(int Total, IReadOnlyList<WbsScheduleRow> Items)> SchedulesAsync(
        string? userId, DateOnly? from, DateOnly? to, string? keyword, int take,
        CancellationToken ct = default)
    {
        await using var db = new NpgsqlConnection(_connectionString);

        var parameters = new
        {
            userId = userId ?? string.Empty,
            from,
            to,
            keyword = keyword ?? string.Empty,
            take,
        };

        const string Where = """
             WHERE (@userId = '' OR a.dev_user = @userId OR a.build_user = @userId OR a.qc_user = @userId)
               AND (@from::date IS NULL OR a.plan_edt IS NULL OR a.plan_edt >= @from)
               AND (@to::date   IS NULL OR a.plan_sdt IS NULL OR a.plan_sdt <= @to)
               AND (@keyword = '' OR a.proc_nm ILIKE '%' || @keyword || '%'
                                  OR a.comm    ILIKE '%' || @keyword || '%')
            """;

        var total = await db.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(*) FROM projmng.dev_wbs a {Where}
            """, parameters, cancellationToken: ct));

        var rows = await db.QueryAsync<WbsScheduleRow>(new CommandDefinition($"""
            SELECT a.proc_nm    AS ProcNm,
                   a.plan_sdt   AS PlanSdt,
                   a.plan_edt   AS PlanEdt,
                   a.dev_user   AS DevUser,
                   a.build_user AS BuildUser,
                   a.qc_user    AS QcUser,
                   a.dev_chk    AS DevChk,
                   a.build_chk  AS BuildChk,
                   a.qc_chk     AS QcChk
              FROM projmng.dev_wbs a
            {Where}
             ORDER BY a.plan_sdt NULLS LAST, a.wbs_id
             LIMIT @take
            """, parameters, cancellationToken: ct));

        return (total, [.. rows]);
    }

    /// <summary>할 일(<c>projmng.home_todo</c>) 한 줄 — 내부 조회용 투영. 사람은 <c>target_user</c> 하나뿐이라
    /// 역할 매칭이 필요 없다.</summary>
    public sealed record HomeTodoRow(
        string? Title, DateOnly? TargetDay, bool IsComplete, string? TargetUser, string? Comments);

    /// <summary>할 일 목록의 전체 건수와 한 쪽을 함께 돌려준다.</summary>
    public async Task<(int Total, IReadOnlyList<HomeTodoRow> Items)> TodosAsync(
        string? userId, DateOnly? from, DateOnly? to, bool? done, int take,
        CancellationToken ct = default)
    {
        await using var db = new NpgsqlConnection(_connectionString);

        var parameters = new
        {
            userId = userId ?? string.Empty,
            from,
            to,
            done,
            take,
        };

        const string Where = """
             WHERE (@userId = '' OR a.target_user = @userId)
               AND (@from::date IS NULL OR a.target_day IS NULL OR a.target_day >= @from)
               AND (@to::date   IS NULL OR a.target_day IS NULL OR a.target_day <= @to)
               AND (@done::boolean IS NULL OR a.is_complete = @done)
            """;

        var total = await db.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(*) FROM projmng.home_todo a {Where}
            """, parameters, cancellationToken: ct));

        var rows = await db.QueryAsync<HomeTodoRow>(new CommandDefinition($"""
            SELECT a.title       AS Title,
                   a.target_day  AS TargetDay,
                   a.is_complete AS IsComplete,
                   a.target_user AS TargetUser,
                   a.comments    AS Comments
              FROM projmng.home_todo a
            {Where}
             ORDER BY a.target_day NULLS LAST, a.todo_key
             LIMIT @take
            """, parameters, cancellationToken: ct));

        return (total, [.. rows]);
    }
}
