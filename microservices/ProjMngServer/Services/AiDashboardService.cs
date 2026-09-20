using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// AI 작업 대시보드의 집계 — <c>projmng.ai_task</c> · <c>ai_task_run</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>세는 단위는 「실행」이다.</b> 「지시」가 아니다 — 한 지시를 세 번 돌리면
/// 처리한 일은 셋이고, 그 셋의 처리시간과 성패가 서로 다르다. 지시로 세면
/// 재시도가 통째로 안 보이고, 그런데 이 기능에서 사람이 가장 알고 싶어 하는
/// 것 중 하나가 「얼마나 다시 돌리고 있나」다.
/// </para>
/// <para>
/// <b>빈 칸을 지우지 않는다.</b> 일별·시간대별은 자료가 없는 칸도 0 으로 채워
/// 돌려준다. 있는 것만 주면 차트의 가로축이 들쭉날쭉해져 <i>「아무 일 없던
/// 사흘」이 아예 없던 일</i>처럼 보인다 — 추세를 보는 화면에서 가장 나쁜 거짓말이다.
/// </para>
/// <para>
/// <b>기간을 타는 것과 「지금」을 갈라 둔다.</b> 대기·실행 중·미확인은 언제로
/// 조회하든 지금 값이다. 섞으면 지난달로 조회한 화면이 「실행 중 0건」이라고
/// 말하면서 실제로는 지금 돌고 있는 일이 생긴다.
/// </para>
/// </remarks>
public sealed class AiDashboardService(
    IConfiguration configuration, ILogger<AiDashboardService> logger)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>예상치의 근거 기간(일). 화면에도 이 숫자를 그대로 적어 준다.</summary>
    private const int BasisDays = 28;

    /// <summary>
    /// 실행기가 살아 있다고 볼 시간. <b>사용량 보고 주기보다 넉넉해야 한다</b> —
    /// 실행기는 15분에 한 번 보고하므로 5분으로 재면 늘 죽어 보인다.
    /// </summary>
    private const int AliveMinutes = 60;

    /// <summary>
    /// 기간 안의 실행만 고른다. <b>모든 집계가 이 조건을 그대로 쓴다</b> —
    /// 한 군데만 달라지면 머리 숫자와 차트의 합이 안 맞는다.
    /// </summary>
    private const string Scope = """
          FROM projmng.ai_task_run r
          JOIN projmng.ai_task t ON t.task_key = r.task_key
          LEFT JOIN projmng.ai_target g ON g.target_key = t.target_key
         WHERE t.is_deleted = false
           AND r.started_at >= @from
           AND r.started_at <  @to
        """;

    /// <summary>처리시간(초). 아직 안 끝난 실행은 <c>NULL</c> 이라 평균에서 저절로 빠진다.</summary>
    private const string Duration = "EXTRACT(EPOCH FROM (r.finished_at - r.started_at))";

    /// <summary>
    /// 대시보드 한 판.
    /// </summary>
    /// <param name="from">시작(포함). 날짜로 잘라 쓴다.</param>
    /// <param name="to">끝(포함). 안에서 하루를 더해 <b>미만</b>으로 바꾼다.</param>
    public async Task<AiDashboardData> LoadAsync(DateTime? from, DateTime? to)
    {
        // 기본은 최근 30일. 자정으로 잘라야 일별 칸과 경계가 맞는다.
        var end = (to?.Date ?? DateTime.Today).AddDays(1);
        var start = from?.Date ?? end.AddDays(-30);

        if (start >= end)
        {
            start = end.AddDays(-1);
        }

        var args = new { from = start, to = end };

        using var db = Open();

        var data = new AiDashboardData { From = start, To = end.AddDays(-1) };

        data.Summary = await SummaryAsync(db, args);
        data.Daily = await DailyAsync(db, args);
        data.Monthly = await MonthlyAsync(db);
        data.Hourly = await HourlyAsync(db, args);
        data.Weekday = await WeekdayAsync(db, args);
        data.ByStatus = await SliceAsync(db, args, "COALESCE(NULLIF(r.run_status, ''), '(없음)')");
        data.ByRunner = await SliceAsync(db, args, "COALESCE(NULLIF(t.runner_kind, ''), '(없음)')");
        data.ByTarget = await SliceAsync(db, args, "COALESCE(NULLIF(g.target_nm, ''), '(대상 없음)')");
        data.ByRequester = await SliceAsync(db, args, "COALESCE(NULLIF(t.cre_id, ''), '(알 수 없음)')");
        data.ByDuration = await DurationAsync(db, args);
        data.Forecast = await ForecastAsync(db);
        data.Usage = await UsageAsync(db);
        data.Recent = await RecentAsync(db, args);

        return data;
    }

    // ── 머리 숫자 ───────────────────────────────────────────

    private async Task<AiDashboardSummary> SummaryAsync(IDbConnection db, object args)
    {
        var row = await db.QuerySingleAsync<SummaryRow>($"""
            SELECT COUNT(*)::int                                            AS Runs,
                   COUNT(DISTINCT r.task_key)::int                          AS Tasks,
                   COUNT(*) FILTER (WHERE r.run_status = 'succeeded')::int   AS Succeeded,
                   COUNT(*) FILTER (WHERE r.run_status = 'failed')::int      AS Failed,
                   COUNT(*) FILTER (WHERE r.run_status = 'timeout')::int     AS Timeout,
                   COUNT(*) FILTER (WHERE r.run_status = 'canceled')::int    AS Canceled,
                   COUNT(*) FILTER (WHERE r.run_status = 'interrupted')::int AS Interrupted,
                   COUNT(*) FILTER (WHERE r.seq > 1)::int                    AS RetryRuns,
                   -- 성공률의 분모다. **아직 도는 것을 실패로 세면 안 된다.**
                   COUNT(*) FILTER (WHERE r.run_status IN
                        ('succeeded','failed','timeout','canceled','interrupted'))::int AS FinishedRuns,
                   COALESCE(ROUND(AVG({Duration})::numeric, 1), 0)          AS AvgSeconds,
                   COALESCE(ROUND((percentile_cont(0.5) WITHIN GROUP (
                        ORDER BY {Duration}))::numeric, 1), 0)              AS MedianSeconds,
                   COALESCE(ROUND((percentile_cont(0.9) WITHIN GROUP (
                        ORDER BY {Duration}))::numeric, 1), 0)              AS P90Seconds,
                   COALESCE(ROUND(MAX({Duration})::numeric, 1), 0)          AS MaxSeconds,
                   COALESCE(ROUND(SUM({Duration})::numeric, 1), 0)          AS TotalSeconds
            {Scope}
            """, args);

        var summary = new AiDashboardSummary
        {
            Runs = row.Runs,
            Tasks = row.Tasks,
            Succeeded = row.Succeeded,
            Failed = row.Failed,
            Timeout = row.Timeout,
            Canceled = row.Canceled,
            Interrupted = row.Interrupted,
            AvgSeconds = row.AvgSeconds,
            MedianSeconds = row.MedianSeconds,
            P90Seconds = row.P90Seconds,
            MaxSeconds = row.MaxSeconds,
            TotalSeconds = row.TotalSeconds,
            SuccessRate = Rate(row.Succeeded, row.FinishedRuns),
            RetryRate = Rate(row.RetryRuns, row.Runs),
        };

        summary.CreatedTasks = await db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
              FROM projmng.ai_task
             WHERE is_deleted = false
               AND cre_dt >= @from AND cre_dt < @to
            """, args);

        // push 는 실행이 아니라 **작업**에 적힌다(`ai_task.pushed_commit`).
        // 비어 있지 않으면 그 건이 운영 배포를 일으켰다는 뜻이다.
        summary.Pushed = await db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
              FROM projmng.ai_task
             WHERE is_deleted = false
               AND pushed_commit IS NOT NULL
               AND finished_at >= @from AND finished_at < @to
            """, args);

        // ── 여기부터는 「지금」이다. 기간을 타지 않는다. ──────
        var now = await db.QuerySingleAsync<NowRow>($"""
            SELECT COUNT(*) FILTER (WHERE request_flag = 'requested'
                                      AND task_status IN ('idle','queued'))::int AS QueuedNow,
                   COUNT(*) FILTER (WHERE task_status IN ('preparing','running'))::int AS RunningNow,
                   COUNT(*) FILTER (WHERE task_status IN
                        ('succeeded','failed','timeout','interrupted')
                        AND user_confirmed = false)::int AS UnconfirmedNow
              FROM projmng.ai_task
             WHERE is_deleted = false
            """);

        summary.QueuedNow = now.QueuedNow;
        summary.RunningNow = now.RunningNow;
        summary.UnconfirmedNow = now.UnconfirmedNow;

        // 실행기의 소식. **이 표를 쓰는 곳은 사용량 보고뿐이다** —
        // 보고가 곧 「살아 있다」는 신호다(AiUsageService.SaveAsync).
        var runner = await db.QuerySingleAsync<RunnerRow>($"""
            SELECT MAX(last_seen_at) AS RunnerSeenAt,
                   COUNT(*) FILTER (WHERE last_seen_at >= now()
                        - make_interval(mins => {AliveMinutes}))::int AS RunnersAlive
              FROM projmng.ai_runner
             WHERE is_enabled = true
            """);

        summary.RunnerSeenAt = runner.RunnerSeenAt;
        summary.RunnersAlive = runner.RunnersAlive;

        return summary;
    }

    // ── 일별 · 월별 ─────────────────────────────────────────

    /// <summary>일별. <b>빈 날도 0 으로 채운다</b> — 머리말 참고.</summary>
    private async Task<List<AiDashboardPoint>> DailyAsync(IDbConnection db, object args)
        => [.. await db.QueryAsync<AiDashboardPoint>($"""
            SELECT to_char(d.day, 'MM-DD')            AS Label,
                   d.day                              AS Bucket,
                   COALESCE(s.runs, 0)::int           AS Runs,
                   COALESCE(s.succeeded, 0)::int      AS Succeeded,
                   COALESCE(s.failed, 0)::int         AS Failed,
                   COALESCE(s.avg_minutes, 0)         AS AvgMinutes,
                   COALESCE(s.total_minutes, 0)       AS TotalMinutes
              FROM generate_series(@from::timestamp, @to::timestamp - interval '1 day',
                                   interval '1 day') AS d(day)
              LEFT JOIN (
                    SELECT date_trunc('day', r.started_at)                  AS bucket,
                           COUNT(*)                                          AS runs,
                           COUNT(*) FILTER (WHERE r.run_status = 'succeeded') AS succeeded,
                           COUNT(*) FILTER (WHERE r.run_status IN
                                ('failed','timeout','interrupted'))          AS failed,
                           ROUND((AVG({Duration}) / 60)::numeric, 1)         AS avg_minutes,
                           ROUND((SUM({Duration}) / 60)::numeric, 1)         AS total_minutes
                    {Scope}
                     GROUP BY 1
              ) s ON s.bucket = d.day
             ORDER BY d.day
            """, args)];

    /// <summary>
    /// 월별 — <b>고른 기간과 무관하게 최근 12개월</b>이다.
    /// </summary>
    /// <remarks>
    /// 기간을 따르게 하면 「최근 7일」로 조회했을 때 월별 차트에 막대가 하나만
    /// 서고, 그 화면에서 월별이 할 말이 없어진다. 월별이 답하는 질문은
    /// 「요즘 늘고 있나」이고 그것은 고른 기간과 다른 질문이다.
    /// </remarks>
    private async Task<List<AiDashboardPoint>> MonthlyAsync(IDbConnection db)
        => [.. await db.QueryAsync<AiDashboardPoint>($"""
            SELECT to_char(m.mon, 'YYYY-MM')          AS Label,
                   m.mon                              AS Bucket,
                   COALESCE(s.runs, 0)::int           AS Runs,
                   COALESCE(s.succeeded, 0)::int      AS Succeeded,
                   COALESCE(s.failed, 0)::int         AS Failed,
                   COALESCE(s.avg_minutes, 0)         AS AvgMinutes,
                   COALESCE(s.total_minutes, 0)       AS TotalMinutes
              FROM generate_series(
                        date_trunc('month', now()) - interval '11 months',
                        date_trunc('month', now()),
                        interval '1 month') AS m(mon)
              LEFT JOIN (
                    SELECT date_trunc('month', r.started_at)                AS bucket,
                           COUNT(*)                                          AS runs,
                           COUNT(*) FILTER (WHERE r.run_status = 'succeeded') AS succeeded,
                           COUNT(*) FILTER (WHERE r.run_status IN
                                ('failed','timeout','interrupted'))          AS failed,
                           ROUND((AVG({Duration}) / 60)::numeric, 1)         AS avg_minutes,
                           ROUND((SUM({Duration}) / 60)::numeric, 1)         AS total_minutes
                      FROM projmng.ai_task_run r
                      JOIN projmng.ai_task t ON t.task_key = r.task_key
                     WHERE t.is_deleted = false
                       AND r.started_at >= date_trunc('month', now()) - interval '11 months'
                     GROUP BY 1
              ) s ON s.bucket = m.mon
             ORDER BY m.mon
            """)];

    // ── 사용 빈도 ───────────────────────────────────────────

    /// <summary>시간대별(0~23시). 24칸을 늘 채운다.</summary>
    private async Task<List<AiDashboardBucket>> HourlyAsync(IDbConnection db, object args)
    {
        var rows = await db.QueryAsync<AiDashboardBucket>($"""
            SELECT h.slot::int              AS Slot,
                   COALESCE(s.runs, 0)::int AS Runs
              FROM generate_series(0, 23) AS h(slot)
              LEFT JOIN (
                    SELECT EXTRACT(HOUR FROM r.started_at)::int AS slot, COUNT(*) AS runs
                    {Scope}
                     GROUP BY 1
              ) s ON s.slot = h.slot
             ORDER BY h.slot
            """, args);

        return [.. rows.Select(r => { r.Label = $"{r.Slot:00}시"; return r; })];
    }

    /// <summary>요일별. <c>EXTRACT(DOW)</c> 는 일요일이 0 이다.</summary>
    private async Task<List<AiDashboardBucket>> WeekdayAsync(IDbConnection db, object args)
    {
        var rows = await db.QueryAsync<AiDashboardBucket>($"""
            SELECT w.slot::int              AS Slot,
                   COALESCE(s.runs, 0)::int AS Runs
              FROM generate_series(0, 6) AS w(slot)
              LEFT JOIN (
                    SELECT EXTRACT(DOW FROM r.started_at)::int AS slot, COUNT(*) AS runs
                    {Scope}
                     GROUP BY 1
              ) s ON s.slot = w.slot
             ORDER BY w.slot
            """, args);

        string[] names = ["일", "월", "화", "수", "목", "금", "토"];

        return [.. rows.Select(r => { r.Label = names[r.Slot % 7]; return r; })];
    }

    // ── 무엇별 ──────────────────────────────────────────────

    /// <summary>
    /// 「무엇별」 집계. 세는 법이 전부 같아서 <b>기준 식만 바꿔 끼운다</b>.
    /// </summary>
    /// <remarks>
    /// <paramref name="keyExpr"/> 는 <b>이 클래스가 부르는 곳에서만 온다</b> —
    /// 바깥에서 받은 글자를 여기 꽂지 않는다. 꽂으면 그 순간 임의 SQL 통로가 된다.
    /// </remarks>
    private async Task<List<AiDashboardSlice>> SliceAsync(
        IDbConnection db, object args, string keyExpr)
        => [.. await db.QueryAsync<AiDashboardSlice>($"""
            SELECT {keyExpr}                                              AS Label,
                   COUNT(*)::int                                          AS Runs,
                   COUNT(*) FILTER (WHERE r.run_status = 'succeeded')::int AS Succeeded,
                   COUNT(*) FILTER (WHERE r.run_status IN
                        ('failed','timeout','interrupted'))::int          AS Failed,
                   COALESCE(ROUND((AVG({Duration}) / 60)::numeric, 1), 0) AS AvgMinutes
            {Scope}
             GROUP BY 1
             ORDER BY 2 DESC, 1
             LIMIT 12
            """, args)];

    /// <summary>
    /// 처리시간 분포.
    /// </summary>
    /// <remarks>
    /// <b>평균 하나로는 「대부분 1분인데 한 건이 세 시간」을 못 본다.</b>
    /// 그리고 그 한 건이 대개 사람이 보고 싶어 하는 건이다.
    /// </remarks>
    private async Task<List<AiDashboardSlice>> DurationAsync(IDbConnection db, object args)
        => [.. await db.QueryAsync<AiDashboardSlice>($"""
            SELECT x.label                                  AS Label,
                   COUNT(*)::int                            AS Runs,
                   COUNT(*) FILTER (WHERE x.ok)::int        AS Succeeded,
                   COUNT(*) FILTER (WHERE NOT x.ok)::int    AS Failed,
                   COALESCE(ROUND((AVG(x.d) / 60)::numeric, 1), 0) AS AvgMinutes
              FROM (
                    SELECT {Duration} AS d,
                           CASE WHEN {Duration} <   60 THEN 1
                                WHEN {Duration} <  300 THEN 2
                                WHEN {Duration} <  900 THEN 3
                                WHEN {Duration} < 1800 THEN 4
                                WHEN {Duration} < 3600 THEN 5
                                ELSE 6 END AS ord,
                           CASE WHEN {Duration} <   60 THEN '1분 미만'
                                WHEN {Duration} <  300 THEN '1~5분'
                                WHEN {Duration} <  900 THEN '5~15분'
                                WHEN {Duration} < 1800 THEN '15~30분'
                                WHEN {Duration} < 3600 THEN '30~60분'
                                ELSE '1시간 이상' END AS label,
                           (r.run_status = 'succeeded') AS ok
                    {Scope}
                       -- 아직 안 끝난 실행은 처리시간이 없다. 0 분으로 세면
                       -- 「1분 미만」이 실제보다 부풀고, 그쪽이 가장 자주 본다.
                       AND r.finished_at IS NOT NULL
              ) x
             GROUP BY x.label, x.ord
             ORDER BY x.ord
            """, args)];

    // ── 예상 사용 건수 ──────────────────────────────────────

    /// <summary>
    /// 최근 <see cref="BasisDays"/> 일의 일평균으로 늘려 잡는다.
    /// </summary>
    /// <remarks>
    /// <b>고른 기간을 쓰지 않는다.</b> 「어제 하루」로 조회한 화면의 예상치가
    /// 어제 한 건을 근거로 나오면 그것은 예상이 아니다.
    /// </remarks>
    private async Task<AiDashboardForecast> ForecastAsync(IDbConnection db)
    {
        var row = await db.QuerySingleAsync<ForecastRow>($"""
            SELECT COUNT(*) FILTER (WHERE r.started_at >= now()
                        - make_interval(days => {BasisDays}))::int          AS BasisRuns,
                   COUNT(*) FILTER (WHERE r.started_at >= date_trunc('day', now()))::int   AS Today,
                   COUNT(*) FILTER (WHERE r.started_at >= date_trunc('week', now()))::int  AS ThisWeek,
                   COUNT(*) FILTER (WHERE r.started_at >= date_trunc('month', now()))::int AS ThisMonth,
                   -- 지난달 **같은 기간**. 달을 통째로 비교하면 3일째인 달이
                   -- 늘 「크게 줄었다」로 나온다.
                   COUNT(*) FILTER (
                        WHERE r.started_at >= date_trunc('month', now()) - interval '1 month'
                          AND r.started_at <  date_trunc('month', now()) - interval '1 month'
                                            + (now() - date_trunc('month', now()))
                   )::int AS LastMonthSoFar
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t ON t.task_key = r.task_key
             WHERE t.is_deleted = false
               AND r.started_at >= date_trunc('month', now()) - interval '2 months'
            """);

        var perDay = Math.Round(row.BasisRuns / (decimal)BasisDays, 2);

        // 오늘이 얼마나 지났나(0~1). 남은 몫을 일평균으로 채운다 —
        // 오늘 실적을 지난 비율로 나누는 방식은 새벽 한 건이 하루 백 건으로
        // 부풀어 예상치가 쓸모없어진다.
        var dayPassed = (decimal)(DateTime.Now - DateTime.Today).TotalDays;
        var daysLeftInMonth = DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month)
            - DateTime.Today.Day;

        return new AiDashboardForecast
        {
            BasisDays = BasisDays,
            BasisRuns = row.BasisRuns,
            PerDay = perDay,
            Today = row.Today,
            TodayExpected = Math.Round(row.Today + (perDay * Math.Max(0, 1 - dayPassed)), 1),
            ThisWeek = row.ThisWeek,
            ThisMonth = row.ThisMonth,
            MonthExpected = Math.Round(row.ThisMonth + (perDay * daysLeftInMonth), 1),
            MonthOverMonth = row.LastMonthSoFar > 0
                ? Math.Round((row.ThisMonth - row.LastMonthSoFar) * 100m / row.LastMonthSoFar, 1)
                : null,
        };
    }

    // ── 곁들이는 것 ─────────────────────────────────────────

    /// <summary>
    /// AI CLI 한도. 실행기가 올려 둔 마지막 값이다.
    /// </summary>
    /// <remarks>
    /// <b>표가 없어도 화면을 죽이지 않는다.</b> 이 표는 곁들이는 칸 하나를
    /// 채울 뿐인데, 운영 반영을 손으로 하는 구조라 「메뉴 SQL 만 돌리고
    /// 한도 SQL 은 안 돌린」 상태가 반드시 생긴다. 그때 대시보드 전체가
    /// 열리지 않으면 고장이 실제보다 훨씬 커 보인다 — 로그로 시끄럽게
    /// 남기고 그 칸만 비운다.
    /// </remarks>
    private async Task<List<AiUsageSnapshot>> UsageAsync(IDbConnection db)
    {
        try
        {
            return [.. await db.QueryAsync<AiUsageSnapshot>("""
            SELECT usage_key          AS UsageKey,
                   runner_nm          AS RunnerNm,
                   runner_kind        AS RunnerKind,
                   ok                 AS Ok,
                   session_pct        AS SessionPct,
                   session_reset_at   AS SessionResetAt,
                   week_pct           AS WeekPct,
                   week_reset_at      AS WeekResetAt,
                   week_opus_pct      AS WeekOpusPct,
                   week_opus_reset_at AS WeekOpusResetAt,
                   limit_tokens       AS LimitTokens,
                   remaining_tokens   AS RemainingTokens,
                   plan_nm            AS PlanNm,
                   raw_text           AS RawText,
                   error_text         AS ErrorText,
                   observed_at        AS ObservedAt
              FROM projmng.ai_usage_snapshot
             ORDER BY runner_kind, runner_nm
            """)];
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            logger.LogWarning(
                "projmng.ai_usage_snapshot 이 없습니다. "
                + "deploy/sql/projmng-ai-usage-2026-09-20.sql 를 돌리십시오.");

            return [];
        }
    }

    private async Task<List<AiDashboardRecent>> RecentAsync(IDbConnection db, object args)
        => [.. await db.QueryAsync<AiDashboardRecent>($"""
            SELECT r.run_key        AS RunKey,
                   r.task_key       AS TaskKey,
                   r.seq            AS Seq,
                   t.title          AS Title,
                   r.run_status     AS RunStatus,
                   t.runner_kind    AS RunnerKind,
                   g.target_nm      AS TargetNm,
                   t.cre_id         AS CreId,
                   r.started_at     AS StartedAt,
                   r.finished_at    AS FinishedAt,
                   ROUND({Duration}::numeric, 1) AS Seconds,
                   r.error_summary  AS ErrorSummary
            {Scope}
             ORDER BY r.started_at DESC
             LIMIT 20
            """, args)];

    private static decimal Rate(int part, int whole)
        => whole <= 0 ? 0 : Math.Round(part * 100m / whole, 1);

    // ── 조회 한 줄을 받는 그릇 ──────────────────────────────

    private sealed class SummaryRow
    {
        public int Runs { get; set; }
        public int Tasks { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Timeout { get; set; }
        public int Canceled { get; set; }
        public int Interrupted { get; set; }
        public int RetryRuns { get; set; }
        public int FinishedRuns { get; set; }
        public decimal AvgSeconds { get; set; }
        public decimal MedianSeconds { get; set; }
        public decimal P90Seconds { get; set; }
        public decimal MaxSeconds { get; set; }
        public decimal TotalSeconds { get; set; }
    }

    private sealed class NowRow
    {
        public int QueuedNow { get; set; }
        public int RunningNow { get; set; }
        public int UnconfirmedNow { get; set; }
    }

    private sealed class RunnerRow
    {
        public DateTime? RunnerSeenAt { get; set; }
        public int RunnersAlive { get; set; }
    }

    private sealed class ForecastRow
    {
        public int BasisRuns { get; set; }
        public int Today { get; set; }
        public int ThisWeek { get; set; }
        public int ThisMonth { get; set; }
        public int LastMonthSoFar { get; set; }
    }
}
