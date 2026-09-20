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

        foreach (var item in report.Items ?? [])
        {
            var kind = item.RunnerKind?.Trim();

            if (string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            await db.ExecuteAsync("""
                INSERT INTO projmng.ai_usage_snapshot
                     ( runner_nm, runner_kind, ok,
                       session_pct, session_reset_at,
                       week_pct, week_reset_at,
                       week_opus_pct, week_opus_reset_at,
                       limit_tokens, remaining_tokens, plan_nm,
                       raw_text, error_text, observed_at )
                VALUES ( @runner, @kind, @ok,
                         @sessionPct, @sessionResetAt,
                         @weekPct, @weekResetAt,
                         @weekOpusPct, @weekOpusResetAt,
                         @limitTokens, @remainingTokens, @plan,
                         @raw, @error, now() )
                ON CONFLICT (runner_nm, runner_kind) DO UPDATE
                   SET ok                 = EXCLUDED.ok,
                       session_pct        = EXCLUDED.session_pct,
                       session_reset_at   = EXCLUDED.session_reset_at,
                       week_pct           = EXCLUDED.week_pct,
                       week_reset_at      = EXCLUDED.week_reset_at,
                       week_opus_pct      = EXCLUDED.week_opus_pct,
                       week_opus_reset_at = EXCLUDED.week_opus_reset_at,
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
                ok = item.Ok,
                sessionPct = Pct(item.SessionPct),
                sessionResetAt = item.SessionResetAt,
                weekPct = Pct(item.WeekPct),
                weekResetAt = item.WeekResetAt,
                weekOpusPct = Pct(item.WeekOpusPct),
                weekOpusResetAt = item.WeekOpusResetAt,
                limitTokens = item.LimitTokens,
                remainingTokens = item.RemainingTokens,
                plan = Trim(item.PlanNm, 50),
                raw = Trim(item.RawText, MaxRawLength),
                error = Trim(item.ErrorText, 1000),
            }, tx);

            saved++;
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

/// <summary>CLI 하나의 한도.</summary>
public sealed class AiUsageItem
{
    public string? RunnerKind { get; set; }

    public bool Ok { get; set; } = true;

    public decimal? SessionPct { get; set; }
    public DateTime? SessionResetAt { get; set; }
    public decimal? WeekPct { get; set; }
    public DateTime? WeekResetAt { get; set; }
    public decimal? WeekOpusPct { get; set; }
    public DateTime? WeekOpusResetAt { get; set; }
    public long? LimitTokens { get; set; }
    public long? RemainingTokens { get; set; }
    public string? PlanNm { get; set; }
    public string? RawText { get; set; }
    public string? ErrorText { get; set; }
}
