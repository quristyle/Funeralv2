using Dapper;
using Npgsql;

using JSini.Shared.Internal;

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
    /// 일정(<c>projmng.dev_wbs</c>) 한 줄. 개발·빌드·검수 세 사람을 그대로 들고 와서 컨트롤러가
    /// 역할 매칭을 계산한다 — SQL 안에서 사람 비교까지 다 해 버리면 WbsRoleMatch 가 쓸 bool 셋을
    /// 다시 못 얻는다. <c>StartDate</c>·<c>EndDate</c>·<c>WbsState</c> 는 화면(WbsService)과
    /// 완전히 같은 식으로 미리 계산해 둔다 — 여기서 다르게 셈하면 같은 일감을 두고 화면과
    /// 비서의 답이 어긋난다.
    /// </summary>
    public sealed record WbsScheduleRow(
        string? ProcNm, DateOnly? StartDate, DateOnly? EndDate,
        string? DevUser, string? BuildUser, string? QcUser,
        string? DevChk, string? BuildChk, string? Comm, string WbsState);

    /// <summary>
    /// 일정 목록의 전체 건수와 한 쪽(<paramref name="take"/>)을 함께 돌려준다.
    /// 기간은 겹침으로 본다(계획 종료일이 <paramref name="from"/> 뒤, 계획 시작일이
    /// <paramref name="to"/> 앞) — 일정은 하루짜리가 아니라 구간이라, 시작일만 보면
    /// 그 주에 걸쳐 있는 긴 일감이 빠진다.
    /// </summary>
    /// <param name="userId">포털 로그인 아이디. <c>dev_user</c> 가 로그인 아이디 그대로인 한 사람
    /// (<c>quristyle</c>)에만 걸린다.</param>
    /// <param name="name">사람 이름. 개발·빌드·검수 칸은 대부분 로그인 아이디가 아니라 손으로
    /// 적은 이름이라(군더더기 포함) 이 값으로도 찾을 수 있게 한다.</param>
    public async Task<(int Total, IReadOnlyList<WbsScheduleRow> Items)> SchedulesAsync(
        string? userId, string? name, DateOnly? from, DateOnly? to, string? keyword, int take,
        CancellationToken ct = default)
    {
        await using var db = new NpgsqlConnection(_connectionString);

        var parameters = new
        {
            from,
            to,
            keyword = keyword ?? string.Empty,
        };

        // 계획 날짜가 비어 있으면 개발 종료일로 본다 — WbsService.Dates 와 똑같은 COALESCE 다.
        // 여기서 다르게 메우면 plan_sdt 가 빈 줄이 기간 조회에서만 사라진다(화면엔 보이는데 비서는 못 찾는다).
        // 사람 조건(userId·name)은 여기서 걸지 않는다 — dev_user/build_user/qc_user 가 손으로 적은
        // 글이라(아래 WbsPersonMatch 참고) SQL 의 단순 비교로는 거를 수 없다.
        const string Where = """
             WHERE (@from::date IS NULL OR COALESCE(a.plan_edt, a.dev_edt) IS NULL
                    OR COALESCE(a.plan_edt, a.dev_edt) >= @from)
               AND (@to::date   IS NULL OR COALESCE(a.plan_sdt, a.dev_edt) IS NULL
                    OR COALESCE(a.plan_sdt, a.dev_edt) <= @to)
               AND (@keyword = '' OR a.proc_nm ILIKE '%' || @keyword || '%'
                                  OR a.comm    ILIKE '%' || @keyword || '%')
            """;

        var rows = await db.QueryAsync<WbsScheduleRow>(new CommandDefinition($"""
            SELECT a.proc_nm                        AS ProcNm,
                   COALESCE(a.plan_sdt, a.dev_edt)   AS StartDate,
                   COALESCE(a.plan_edt, a.dev_edt)   AS EndDate,
                   a.dev_user                        AS DevUser,
                   a.build_user                      AS BuildUser,
                   a.qc_user                         AS QcUser,
                   a.dev_chk                         AS DevChk,
                   a.build_chk                       AS BuildChk,
                   a.comm                            AS Comm,
                   -- WbsService.Columns 의 WbsState 와 한 글자도 다르지 않게 옮긴 식이다.
                   -- dev_chk/build_chk/qc_chk 는 참거짓 칸이 아니라 자유 글(메모)이라 완료
                   -- 판정에 못 쓴다 — 진짜 완료는 개발 종료일(dev_edt)의 유무로만 갈린다.
                   CASE WHEN COALESCE(a.dev_sdt, a.dev_edt) IS NULL THEN 'READY'
                        WHEN a.dev_edt IS NULL                      THEN 'RUNNING'
                        ELSE 'COMP'
                   END                                AS WbsState
              FROM projmng.dev_wbs a
            {Where}
             ORDER BY COALESCE(a.plan_sdt, a.dev_edt) NULLS LAST, a.wbs_id
            """, parameters, cancellationToken: ct));

        // 날짜·키워드만 SQL 로 거르고(표가 수백 행뿐이라 전부 읽어도 가볍다), 사람 조건은 여기서
        // WbsPersonMatch 로 가른다 — 아이디(userId)·이름(name) 중 아무거나로, 세 칸(개발·빌드·검수)
        // 아무 데나 걸리면 포함한다.
        var matched = string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(name)
            ? [.. rows]
            : rows.Where(r => MatchesPerson(r, userId) || MatchesPerson(r, name)).ToList();

        return (matched.Count, [.. matched.Take(take)]);
    }

    /// <summary>한 줄의 개발·빌드·검수 칸 중 하나라도 후보(아이디 또는 이름)를 가리키는가.</summary>
    private static bool MatchesPerson(WbsScheduleRow row, string? candidate) =>
        WbsPersonMatch.Matches(row.DevUser, candidate)
        || WbsPersonMatch.Matches(row.BuildUser, candidate)
        || WbsPersonMatch.Matches(row.QcUser, candidate);

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
