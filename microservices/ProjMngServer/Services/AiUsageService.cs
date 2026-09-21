using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 실행기가 올려 주는 AI CLI 한도를 받아 적는다 — <c>projmng.ai_usage_snapshot</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>서버가 CLI 를 부르지 않는다.</b> `/usage` 를 아는 것은 CLI 이고 그것은
/// 실행기 장비에만 있다. 웹 요청을 받는 프로세스에 셸을 여는 권한을 두지
/// 않는 것이 이 기능 전체의 전제다(<c>docs/ai-task-runner.md</c>).
/// </para>
/// <para>
/// <b>보고가 곧 「살아 있다」는 신호다.</b> 같은 요청으로 <c>ai_runner</c> 의
/// <c>last_seen_at</c> 을 함께 찍는다 — 그 표에는 지금껏 아무도 쓰지 않아서
/// 「실행기가 붙어 있나」를 화면이 말할 방법이 없었다.
/// </para>
/// </remarks>
public sealed class AiUsageService(IConfiguration configuration, ILogger<AiUsageService> logger)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>한 줄의 길이 상한. CLI 가 화면 하나를 통째로 뱉어도 표가 붓지 않게.</summary>
    private const int MaxRawLength = 4000;

    /// <summary>
    /// 보고 한 번을 받아 적는다.
    /// </summary>
    /// <returns>실제로 적은 한도 줄 수.</returns>
    public async Task<int> SaveAsync(AiUsageReport report)
    {
        var runner = report.RunnerName?.Trim();

        if (string.IsNullOrWhiteSpace(runner))
        {
            return 0;
        }

        using var db = Open();
        db.Open();

        using var tx = db.BeginTransaction();

        // ① 장비가 살아 있다. **한도를 하나도 못 읽었어도 이것은 적는다** —
        //    「실행기는 붙어 있는데 사용량만 못 읽는 중」이 실제로 있는 상태다.
        await db.ExecuteAsync("""
            INSERT INTO projmng.ai_runner (runner_nm, host_nm, runner_kinds, last_seen_at, runner_version)
            VALUES (@runner, @host, @kinds, now(), @version)
            ON CONFLICT (runner_nm) DO UPDATE
               SET host_nm        = COALESCE(EXCLUDED.host_nm, projmng.ai_runner.host_nm),
                   runner_kinds   = COALESCE(NULLIF(EXCLUDED.runner_kinds, ''),
                                             projmng.ai_runner.runner_kinds),
                   runner_version = COALESCE(EXCLUDED.runner_version, projmng.ai_runner.runner_version),
                   last_seen_at   = now()
            """, new
        {
            runner,
            host = Trim(report.HostName, 200),
            kinds = report.Kinds is { Count: > 0 }
                ? string.Join(',', report.Kinds.Where(k => !string.IsNullOrWhiteSpace(k)))
                : string.Empty,
            version = Trim(report.RunnerVersion, 50),
        }, tx);

        var saved = 0;

        // 이번 보고에 실제로 실려 온 칸. **지워진 칸을 치우는 데 쓴다**(아래).
        var seen = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var item in report.Items ?? [])
        {
            var kind = item.RunnerKind?.Trim();

            if (string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            // 하나뿐인 CLI 는 빈 글자다. **NULL 로 두지 않는다** — 열쇠에
            // 들어가는 칸이라 NULL 이면 덮어쓰기가 안 걸려 줄이 계속 쌓인다.
            var bucket = Trim(item.BucketNm, 60) ?? string.Empty;

            await db.ExecuteAsync("""
                INSERT INTO projmng.ai_usage_snapshot
                     ( runner_nm, runner_kind, bucket_nm, ok,
                       session_pct, session_reset_at,
                       week_pct, week_reset_at,
                       week_opus_pct, week_opus_reset_at,
                       month_pct, month_reset_at,
                       limit_tokens, remaining_tokens, plan_nm,
                       raw_text, error_text, observed_at )
                VALUES ( @runner, @kind, @bucket, @ok,
                         @sessionPct, @sessionResetAt,
                         @weekPct, @weekResetAt,
                         @weekOpusPct, @weekOpusResetAt,
                         @monthPct, @monthResetAt,
                         @limitTokens, @remainingTokens, @plan,
                         @raw, @error, now() )
                ON CONFLICT (runner_nm, runner_kind, bucket_nm) DO UPDATE
                   SET ok                 = EXCLUDED.ok,
                       session_pct        = EXCLUDED.session_pct,
                       session_reset_at   = EXCLUDED.session_reset_at,
                       week_pct           = EXCLUDED.week_pct,
                       week_reset_at      = EXCLUDED.week_reset_at,
                       week_opus_pct      = EXCLUDED.week_opus_pct,
                       week_opus_reset_at = EXCLUDED.week_opus_reset_at,
                       month_pct          = EXCLUDED.month_pct,
                       month_reset_at     = EXCLUDED.month_reset_at,
                       limit_tokens       = EXCLUDED.limit_tokens,
                       remaining_tokens   = EXCLUDED.remaining_tokens,
                       plan_nm            = EXCLUDED.plan_nm,
                       raw_text           = EXCLUDED.raw_text,
                       error_text         = EXCLUDED.error_text,
                       observed_at        = now(),
                       mod_dt             = now()
                """, new
            {
                runner,
                kind,
                bucket,
                ok = item.Ok,
                sessionPct = Pct(item.SessionPct),
                sessionResetAt = item.SessionResetAt,
                weekPct = Pct(item.WeekPct),
                weekResetAt = item.WeekResetAt,
                weekOpusPct = Pct(item.WeekOpusPct),
                weekOpusResetAt = item.WeekOpusResetAt,
                monthPct = Pct(item.MonthPct),
                monthResetAt = item.MonthResetAt,
                limitTokens = item.LimitTokens,
                remainingTokens = item.RemainingTokens,
                plan = Trim(item.PlanNm, 50),
                raw = Trim(item.RawText, MaxRawLength),
                error = Trim(item.ErrorText, 1000),
            }, tx);

            if (!seen.TryGetValue(kind, out var buckets))
            {
                seen[kind] = buckets = [];
            }

            buckets.Add(bucket);
            saved++;
        }

        // ③ **이번에 안 온 칸은 치운다.** CLI 가 모델군 이름을 바꾸거나
        //    한도 종류를 하나 접으면, 옛 줄이 아무도 갱신하지 않는 채
        //    화면에 남는다. 그 줄에는 「언제 기준」이 옛 시각으로 찍혀 있어
        //    「오래된 값」 딱지가 붙는데, 사람은 그것을 **실행기가 멎었다**로
        //    읽는다 — 멀쩡한 장비를 들여다보게 만드는 거짓말이다.
        //
        //    같은 종류를 이번에 하나라도 받았을 때만 치운다. 통째로 못 읽은
        //    주기에 지워 버리면 마지막으로 알던 값까지 잃는다.
        foreach (var (kind, buckets) in seen)
        {
            await db.ExecuteAsync("""
                DELETE FROM projmng.ai_usage_snapshot
                 WHERE runner_nm = @runner
                   AND runner_kind = @kind
                   AND bucket_nm <> ALL(@buckets)
                """, new { runner, kind, buckets = buckets.ToArray() }, tx);
        }

        tx.Commit();

        logger.LogInformation("{Runner} 가 사용량 {Count}건을 올렸습니다.", runner, saved);

        return saved;
    }

    /// <summary>
    /// 퍼센트를 0~100 으로 자른다.
    /// </summary>
    /// <remarks>
    /// 칸이 <c>numeric(5,2)</c> 라 세 자리가 들어오면 <b>보고 전체가 끊긴다.</b>
    /// 출력 형식이 바뀌어 엉뚱한 숫자를 주워 왔을 때 그 한 줄 때문에 장비의
    /// 소식까지 못 받는 쪽이 더 나쁘다.
    /// </remarks>
    private static decimal? Pct(decimal? value)
        => value is null ? null : Math.Clamp(value.Value, 0m, 100m);

    private static string? Trim(string? text, int max)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : text.Length <= max ? text : text[..max];
}

/// <summary>실행기가 보내는 사용량 보고 한 번.</summary>
public sealed class AiUsageReport
{
    public string? RunnerName { get; set; }

    public string? HostName { get; set; }

    public string? RunnerVersion { get; set; }

    /// <summary>이 장비가 돌릴 수 있는 CLI. 표에 그대로 적어 둔다.</summary>
    public List<string>? Kinds { get; set; }

    /// <summary>읽어 온 한도들. <b>비어 있을 수 있다</b> — 그래도 장비 소식은 남는다.</summary>
    public List<AiUsageItem>? Items { get; set; }
}

/// <summary>
/// 한도 한 칸. <b>CLI 하나가 한 줄이 아니다</b> — <see cref="BucketNm"/> 참고.
/// </summary>
public sealed class AiUsageItem
{
    public string? RunnerKind { get; set; }

    /// <summary>
    /// 같은 CLI 안에서 무엇의 한도인가 — <c>agy</c> 는 모델군, <c>copilot</c> 은
    /// 한도 종류. 하나뿐인 CLI 는 비운다.
    /// </summary>
    public string? BucketNm { get; set; }

    public bool Ok { get; set; } = true;

    public decimal? SessionPct { get; set; }
    public DateTime? SessionResetAt { get; set; }
    public decimal? WeekPct { get; set; }
    public DateTime? WeekResetAt { get; set; }
    public decimal? WeekOpusPct { get; set; }
    public DateTime? WeekOpusResetAt { get; set; }

    /// <summary>월간 한도 사용률(%). 달로 끊는 CLI(<c>copilot</c>)가 쓴다.</summary>
    public decimal? MonthPct { get; set; }

    public DateTime? MonthResetAt { get; set; }
    public long? LimitTokens { get; set; }
    public long? RemainingTokens { get; set; }
    public string? PlanNm { get; set; }
    public string? RawText { get; set; }
    public string? ErrorText { get; set; }
}
