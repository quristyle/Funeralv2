using System.Data;

using Dapper;
using JSini.Shared.Infrastructure.Time;
using Npgsql;
using ProjModel;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>AI 작업 대시보드 집계(ai_task · ai_task_run). 세는 단위는 「실행」이다.</summary>
/// 빈 날짜 칸은 0 으로 채우고, 대기·실행 중처럼 지금 값인 것은 기간과 무관하게 따로 센다.
public sealed class AiDashboardService(
    IConfiguration configuration, ILogger<AiDashboardService> logger, AiUsageCache usageCache)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>예상치의 근거 기간(일). 화면에도 이 숫자를 그대로 적어 준다.</summary>
    private const int BasisDays = 28;

    /// <summary>실행기가 살아 있다고 볼 시간. 사용량 보고 주기(15분)보다 넉넉해야 한다.</summary>
    private const int AliveMinutes = 60;

    /// <summary>시작 시각을 한국 벽시계로 본 것. 날짜·요일·시간대 칸을 가르는 기준이다.</summary>
    /// 저장과 견주기는 UTC, 달력 칸 가르기만 한국 시각이다(docs/utc-time.md). UTC 로 가르면 오전 9시 전이 전날 칸으로 밀린다.
    private const string StartedKst = "(r.started_at AT TIME ZONE 'Asia/Seoul')";

    /// <summary>지금을 한국 벽시계로. <see cref="StartedKst"/> 와 견주는 값이다.</summary>
    private const string NowKst = "(now() AT TIME ZONE 'Asia/Seoul')";

    /// <summary>기간 안의 실행만 고른다. 모든 집계가 이 조건을 그대로 쓴다.</summary>
    /// @from · @to 는 한국 달력 날짜라 한국 벽시계끼리 견준다.
    private const string Scope = $"""
          FROM projmng.ai_task_run r
          JOIN projmng.ai_task t ON t.task_key = r.task_key
          LEFT JOIN projmng.ai_target g ON g.target_key = t.target_key
         WHERE t.is_deleted = false
           AND {StartedKst} >= @from
           AND {StartedKst} <  @to
        """;

    /// <summary>처리시간(초). 아직 안 끝난 실행은 <c>NULL</c> 이라 평균에서 저절로 빠진다.</summary>
    private const string Duration = "EXTRACT(EPOCH FROM (r.finished_at - r.started_at))";

    /// <summary>대시보드 한 판.</summary>
    /// <param name="from">시작(포함). 날짜로 잘라 쓴다.</param>
    /// <param name="to">끝(포함). 안에서 하루를 더해 미만으로 바꾼다.</param>
    public async Task<AiDashboardData> LoadAsync(DateTime? from, DateTime? to)
    {
        // 기본은 최근 30일. 자정으로 잘라야 일별 칸과 경계가 맞는다.
        // 「오늘」은 **한국 달력의 오늘**이다 — 화면이 고르는 것이 달력 날짜다.
        var end = (to?.Date ?? AppTime.ToKorea(AppTime.UtcNow).Date).AddDays(1);
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
        data.Forecast = await ForecastAsync(db, data.Summary.TodayTokens);
        data.Usage = await UsageAsync(db);
        data.UsageHourly = await UsageHourlyAsync(db);
        data.UsageDaily = await UsageDailyAsync(db, args);
        data.UsageMonthly = await UsageMonthlyAsync(db);
        data.Recent = await RecentAsync(db, args);

        return data;
    }

    /// <summary>AI CLI 한도만 따로 읽는다. 대시보드 한 판을 부르지 않으려고 낸 자리다.</summary>
    /// 같은 UsageAsync 를 쓰고 원문(RawText)은 떼고 준다 — 화면은 퍼센트만 쓴다.
    public async Task<List<AiUsageSnapshot>> LoadUsageAsync()
    {
        if (usageCache.Get() is { } cached)
        {
            return cached;
        }

        using var db = Open();

        var rows = await UsageAsync(db);

        foreach (var row in rows)
        {
            row.RawText = null;
        }

        usageCache.Put(rows);

        return rows;
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

        summary.CreatedTasks = await db.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)::int
              FROM projmng.ai_task
             WHERE is_deleted = false
               AND (cre_dt AT TIME ZONE 'Asia/Seoul') >= @from
               AND (cre_dt AT TIME ZONE 'Asia/Seoul') <  @to
            """, args);

        // push 는 실행이 아니라 **작업**에 적힌다(`ai_task.pushed_commit`).
        // 비어 있지 않으면 그 건이 운영 배포를 일으켰다는 뜻이다.
        summary.Pushed = await db.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*)::int
              FROM projmng.ai_task
             WHERE is_deleted = false
               AND pushed_commit IS NOT NULL
               AND (finished_at AT TIME ZONE 'Asia/Seoul') >= @from
               AND (finished_at AT TIME ZONE 'Asia/Seoul') <  @to
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

        // 오늘 AI 가 돈 시간의 합(초). 기간 설정과 무관하게 한국 달력의 오늘이다.
        summary.TodayTotalSeconds = await db.ExecuteScalarAsync<decimal>($"""
            SELECT COALESCE(ROUND(SUM({Duration})::numeric, 1), 0)
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t ON t.task_key = r.task_key
             WHERE t.is_deleted = false
               AND {StartedKst} >= date_trunc('day', {NowKst})
            """);

        // 오늘 및 기간 사용 토큰 (포털 DB scom.ai_usage_logs).
        await PopulateTokensAsync(db, args, summary);

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
                    SELECT date_trunc('day', {StartedKst})                 AS bucket,
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

    /// <summary>월별 — 고른 기간과 무관하게 최근 12개월이다.</summary>
    /// 기간을 따르게 하면 월별 차트가 막대 하나로 무너지므로 기간과 무관하다.
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
                        date_trunc('month', {NowKst}) - interval '11 months',
                        date_trunc('month', {NowKst}),
                        interval '1 month') AS m(mon)
              LEFT JOIN (
                    SELECT date_trunc('month', {StartedKst})              AS bucket,
                           COUNT(*)                                          AS runs,
                           COUNT(*) FILTER (WHERE r.run_status = 'succeeded') AS succeeded,
                           COUNT(*) FILTER (WHERE r.run_status IN
                                ('failed','timeout','interrupted'))          AS failed,
                           ROUND((AVG({Duration}) / 60)::numeric, 1)         AS avg_minutes,
                           ROUND((SUM({Duration}) / 60)::numeric, 1)         AS total_minutes
                      FROM projmng.ai_task_run r
                      JOIN projmng.ai_task t ON t.task_key = r.task_key
                     WHERE t.is_deleted = false
                       AND {StartedKst} >= date_trunc('month', {NowKst}) - interval '11 months'
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
                    SELECT EXTRACT(HOUR FROM {StartedKst})::int AS slot, COUNT(*) AS runs
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
                    SELECT EXTRACT(DOW FROM {StartedKst})::int AS slot, COUNT(*) AS runs
                    {Scope}
                     GROUP BY 1
              ) s ON s.slot = w.slot
             ORDER BY w.slot
            """, args);

        string[] names = ["일", "월", "화", "수", "목", "금", "토"];

        return [.. rows.Select(r => { r.Label = names[r.Slot % 7]; return r; })];
    }

    // ── 무엇별 ──────────────────────────────────────────────

    /// <summary>무엇별 집계. 세는 법이 같아서 기준 식만 바꿔 끼운다.</summary>
    /// keyExpr 는 이 클래스가 부르는 곳에서만 온다 — 바깥 글자를 꽂으면 임의 SQL 통로가 된다.
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

    /// <summary>처리시간 분포. 평균 하나로는 한 건의 긴 실행이 묻힌다.</summary>
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

    /// <summary>최근 BasisDays 일의 일평균으로 늘려 잡는다.</summary>
    /// 고른 기간을 쓰면 하루만 조회했을 때 어제 한 건이 예상치가 된다.
    private async Task<AiDashboardForecast> ForecastAsync(IDbConnection db, long todayTokens = 0)
    {
        var row = await db.QuerySingleAsync<ForecastRow>($"""
            SELECT COUNT(*) FILTER (WHERE r.started_at >= now()
                        - make_interval(days => {BasisDays}))::int          AS BasisRuns,
                   COUNT(*) FILTER (WHERE {StartedKst} >= date_trunc('day', {NowKst}))::int   AS Today,
                   COUNT(*) FILTER (WHERE {StartedKst} >= date_trunc('week', {NowKst}))::int  AS ThisWeek,
                   COUNT(*) FILTER (WHERE {StartedKst} >= date_trunc('month', {NowKst}))::int AS ThisMonth,
                   -- 지난달 **같은 기간**. 달을 통째로 비교하면 3일째인 달이
                   -- 늘 「크게 줄었다」로 나온다.
                   COUNT(*) FILTER (
                        WHERE {StartedKst} >= date_trunc('month', {NowKst}) - interval '1 month'
                          AND {StartedKst} <  date_trunc('month', {NowKst}) - interval '1 month'
                                            + ({NowKst} - date_trunc('month', {NowKst}))
                   )::int AS LastMonthSoFar
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t ON t.task_key = r.task_key
             WHERE t.is_deleted = false
               AND {StartedKst} >= date_trunc('month', {NowKst}) - interval '2 months'
            """);

        var perDay = Math.Round(row.BasisRuns / (decimal)BasisDays, 2);

        // 오늘이 얼마나 지났나(0~1)로 나누면 새벽 한 건이 하루 백 건으로 부풀어, 남은 몫을 일평균으로 채운다.
        // 위 SQL 이 한국 달력으로 셌으므로 이것도 한국 시각이다.
        var nowKst = AppTime.ToKorea(AppTime.UtcNow);
        var dayPassed = (decimal)(nowKst - nowKst.Date).TotalDays;
        var daysLeftInMonth = DateTime.DaysInMonth(nowKst.Year, nowKst.Month) - nowKst.Day;

        return new AiDashboardForecast
        {
            BasisDays = BasisDays,
            BasisRuns = row.BasisRuns,
            PerDay = perDay,
            Today = row.Today,
            TodayTokens = todayTokens,
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

    /// <summary>AI CLI 한도. 실행기가 올려 둔 마지막 값이다.</summary>
    /// 표가 없어도 화면을 죽이지 않는다 — 한도 표만 빠진 상태가 생길 수 있어서다.
    private async Task<List<AiUsageSnapshot>> UsageAsync(IDbConnection db)
    {
        try
        {
            return [.. await db.QueryAsync<AiUsageSnapshot>("""
            SELECT usage_key          AS UsageKey,
                   runner_nm          AS RunnerNm,
                   runner_kind        AS RunnerKind,
                   bucket_nm          AS BucketNm,
                   ok                 AS Ok,
                   session_pct        AS SessionPct,
                   session_reset_at   AS SessionResetAt,
                   week_pct           AS WeekPct,
                   week_reset_at      AS WeekResetAt,
                   week_opus_pct      AS WeekOpusPct,
                   week_opus_reset_at AS WeekOpusResetAt,
                   month_pct          AS MonthPct,
                   month_reset_at     AS MonthResetAt,
                   limit_tokens       AS LimitTokens,
                   remaining_tokens   AS RemainingTokens,
                   plan_nm            AS PlanNm,
                   auth_expires_at    AS AuthExpiresAt,
                   raw_text           AS RawText,
                   error_text         AS ErrorText,
                   observed_at        AS ObservedAt
              FROM projmng.ai_usage_snapshot
             ORDER BY runner_kind, bucket_nm, runner_nm
            """)];
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            logger.LogWarning(
                "projmng.ai_usage_snapshot 이 없습니다. "
                + "deploy/sql/projmng-ai-usage-2026-09-20.sql 를 돌리십시오.");

            return [];
        }
        catch (PostgresException ex) when (ex.SqlState == "42703")
        {
            // 칸이 모자란 것도 같은 종류의 고장이다. 곁들이는 칸 때문에 대시보드 전체가 안 열리면 고장이 과장돼 보인다.
            logger.LogWarning(
                "projmng.ai_usage_snapshot 에 칸이 모자랍니다({Message}). "
                + "deploy/sql/projmng-ai-usage-bucket-2026-09-21.sql 를 돌리십시오.",
                ex.MessageText);

            return [];
        }
    }

    /// <summary>시간별 AI 모델 사용량 추이(최근 24시간, 주간 사용량 %).</summary>
    /// 한 시각의 한도 줄은 MAX 로 접고 그 값을 시간별로 평균한다 — 줄마다 평균하면 한 주머니만 찬 것이 선을 반으로 내린다.
    private async Task<List<AiUsageTrendPoint>> UsageHourlyAsync(IDbConnection db)
    {
        try
        {
            return [.. await db.QueryAsync<AiUsageTrendPoint>($"""
                SELECT to_char(h.hour, 'HH24:00')                         AS Label,
                       h.hour                                             AS Bucket,
                       COALESCE(s.claude_pct, 0)                          AS ClaudePct,
                       COALESCE(s.antigravity_pct, 0)                     AS AntigravityPct,
                       COALESCE(s.copilot_pct, 0)                         AS CopilotPct,
                       COALESCE(s.avg_pct, 0)                             AS AvgPct
                  FROM generate_series(
                            date_trunc('hour', {NowKst}) - interval '23 hours',
                            date_trunc('hour', {NowKst}),
                            interval '1 hour') AS h(hour)
                  LEFT JOIN (
                        SELECT bucket,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'claude')::numeric, 1) AS claude_pct,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'antigravity')::numeric, 1) AS antigravity_pct,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'copilot')::numeric, 1) AS copilot_pct,
                               ROUND(AVG(pct)::numeric, 1) AS avg_pct
                          FROM (
                                SELECT date_trunc('hour', (observed_at AT TIME ZONE 'Asia/Seoul')) AS bucket,
                                       runner_kind, runner_nm, observed_at,
                                       MAX(COALESCE(week_pct, month_pct)) AS pct
                                  FROM projmng.ai_usage_history
                                 WHERE (observed_at AT TIME ZONE 'Asia/Seoul') >= date_trunc('hour', {NowKst}) - interval '23 hours'
                                 GROUP BY 1, 2, 3, 4
                          ) k
                         GROUP BY 1
                  ) s ON s.bucket = h.hour
                 ORDER BY h.hour
                """)];
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
        {
            logger.LogWarning("projmng.ai_usage_history 를 읽지 못했습니다({Message}).", ex.MessageText);
            return [];
        }
    }

    /// <summary>일별 AI 모델 사용량 추이(조회 기간 기준, 주간 사용량 %).</summary>
    private async Task<List<AiUsageTrendPoint>> UsageDailyAsync(IDbConnection db, object args)
    {
        try
        {
            return [.. await db.QueryAsync<AiUsageTrendPoint>($"""
                SELECT to_char(d.day, 'MM-DD')                            AS Label,
                       d.day                                              AS Bucket,
                       COALESCE(s.claude_pct, 0)                          AS ClaudePct,
                       COALESCE(s.antigravity_pct, 0)                     AS AntigravityPct,
                       COALESCE(s.copilot_pct, 0)                         AS CopilotPct,
                       COALESCE(s.avg_pct, 0)                             AS AvgPct
                  FROM generate_series(@from::timestamp, @to::timestamp - interval '1 day',
                                       interval '1 day') AS d(day)
                  LEFT JOIN (
                        SELECT bucket,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'claude')::numeric, 1) AS claude_pct,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'antigravity')::numeric, 1) AS antigravity_pct,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'copilot')::numeric, 1) AS copilot_pct,
                               ROUND(AVG(pct)::numeric, 1) AS avg_pct
                          FROM (
                                SELECT date_trunc('day', (observed_at AT TIME ZONE 'Asia/Seoul')) AS bucket,
                                       runner_kind, runner_nm, observed_at,
                                       MAX(COALESCE(week_pct, month_pct)) AS pct
                                  FROM projmng.ai_usage_history
                                 WHERE (observed_at AT TIME ZONE 'Asia/Seoul') >= @from
                                   AND (observed_at AT TIME ZONE 'Asia/Seoul') <  @to
                                 GROUP BY 1, 2, 3, 4
                          ) k
                         GROUP BY 1
                  ) s ON s.bucket = d.day
                 ORDER BY d.day
                """, args)];
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
        {
            logger.LogWarning("projmng.ai_usage_history 를 읽지 못했습니다({Message}).", ex.MessageText);
            return [];
        }
    }

    /// <summary>월별 AI 모델 사용량 추이(최근 12개월, 주간 사용량 %).</summary>
    private async Task<List<AiUsageTrendPoint>> UsageMonthlyAsync(IDbConnection db)
    {
        try
        {
            return [.. await db.QueryAsync<AiUsageTrendPoint>($"""
                SELECT to_char(m.mon, 'YYYY-MM')                          AS Label,
                       m.mon                                              AS Bucket,
                       COALESCE(s.claude_pct, 0)                          AS ClaudePct,
                       COALESCE(s.antigravity_pct, 0)                     AS AntigravityPct,
                       COALESCE(s.copilot_pct, 0)                         AS CopilotPct,
                       COALESCE(s.avg_pct, 0)                             AS AvgPct
                  FROM generate_series(
                            date_trunc('month', {NowKst}) - interval '11 months',
                            date_trunc('month', {NowKst}),
                            interval '1 month') AS m(mon)
                  LEFT JOIN (
                        SELECT bucket,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'claude')::numeric, 1) AS claude_pct,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'antigravity')::numeric, 1) AS antigravity_pct,
                               ROUND(AVG(pct) FILTER (WHERE runner_kind = 'copilot')::numeric, 1) AS copilot_pct,
                               ROUND(AVG(pct)::numeric, 1) AS avg_pct
                          FROM (
                                SELECT date_trunc('month', (observed_at AT TIME ZONE 'Asia/Seoul')) AS bucket,
                                       runner_kind, runner_nm, observed_at,
                                       MAX(COALESCE(week_pct, month_pct)) AS pct
                                  FROM projmng.ai_usage_history
                                 WHERE (observed_at AT TIME ZONE 'Asia/Seoul') >= date_trunc('month', {NowKst}) - interval '11 months'
                                 GROUP BY 1, 2, 3, 4
                          ) k
                         GROUP BY 1
                  ) s ON s.bucket = m.mon
                 ORDER BY m.mon
                """)];
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
        {
            logger.LogWarning("projmng.ai_usage_history 를 읽지 못했습니다({Message}).", ex.MessageText);
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

    // ── 토큰 사용량 (포털 DB) ──────────────────────────────

    /// <summary>포털 DB(scom.ai_usage_logs)에서 오늘 및 조회 기간 토큰 사용량을 집계한다.</summary>
    private async Task PopulateTokensAsync(IDbConnection projmngDb, object args, AiDashboardSummary summary)
    {
        try
        {
            var di = await projmngDb.QueryFirstOrDefaultAsync<DbInfo>(
                ConstInfo.dbConQuery, new { db_nick = "jsini-comm" });

            if (di is not null && (di.Db_type == "POSTGRESQL" || di.Db_type == "EDB"))
            {
                using var portalDb = new NpgsqlConnection(di.ToConnectionString());
                var tokenRow = await portalDb.QuerySingleAsync<TokenRow>($"""
                    SELECT COALESCE(SUM(total_tokens) FILTER (
                                WHERE (occurred_at AT TIME ZONE 'Asia/Seoul') >= date_trunc('day', {NowKst})
                           ), 0)::bigint AS TodayTokens,
                           COALESCE(COUNT(*) FILTER (
                                WHERE (occurred_at AT TIME ZONE 'Asia/Seoul') >= date_trunc('day', {NowKst})
                           ), 0)::int AS TodayCalls,
                           COALESCE(SUM(total_tokens) FILTER (
                                WHERE (occurred_at AT TIME ZONE 'Asia/Seoul') >= @from
                                  AND (occurred_at AT TIME ZONE 'Asia/Seoul') <  @to
                           ), 0)::bigint AS PeriodTokens,
                           COALESCE(COUNT(*) FILTER (
                                WHERE (occurred_at AT TIME ZONE 'Asia/Seoul') >= @from
                                  AND (occurred_at AT TIME ZONE 'Asia/Seoul') <  @to
                           ), 0)::int AS PeriodCalls
                      FROM scom.ai_usage_logs
                    """, args);

                summary.TodayTokens = tokenRow.TodayTokens;
                summary.TodayCalls = tokenRow.TodayCalls;
                summary.PeriodTokens = tokenRow.PeriodTokens;
                summary.PeriodCalls = tokenRow.PeriodCalls;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "포털 DB(jsini-comm)에서 토큰 사용량을 읽어오지 못했습니다.");
        }
    }

    private sealed class TokenRow
    {
        public long TodayTokens { get; set; }
        public int TodayCalls { get; set; }
        public long PeriodTokens { get; set; }
        public int PeriodCalls { get; set; }
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
