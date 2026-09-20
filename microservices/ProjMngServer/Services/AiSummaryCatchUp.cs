using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// <b>비어 있는 「처리 요약」을 뒤늦게 채운다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 생겼나 (2026-09-21).</b> 요약을 만들 기회는 실행이 끝나는 그 한 번뿐이었다
/// (<see cref="AiRunService"/> 의 <c>SummarizeThenNotifyAsync</c>). 그 한 번은
/// 결과 메일이 기다리는 자리라 오래 붙잡을 수 없어서 두 번만 시도하고 3초 쉰다 —
/// <b>AI 가 그 30여 초 동안 붐비면 그 건의 요약은 영영 없다.</b> 아무도 다시
/// 만들어 주지 않고, 화면은 요약 칸을 통째로 빼고 그린다. 실제로 사흘치 실행의
/// 절반가량이 그렇게 비어 있었다(95건).
/// </para>
/// <para>
/// <b>그래서 자리를 나눈다.</b> 끝나는 순간의 한 번은 <i>빠른 길</i>로 남겨
/// 메일을 늦추지 않고, 거기서 못 만든 것은 이 감시자가 주워 간다. 요약을 쓰는
/// 일은 <see cref="AiRunSummaryWriter"/> 한 곳이 그대로 맡으므로 —
/// 이미 적혀 있으면 되읽고 끝낸다 — 두 길이 서로 다른 요약을 만들 일은 없다.
/// </para>
/// <para>
/// <b>한 바퀴에 몇 건만 본다.</b> 밀린 것이 수십 건일 때 한꺼번에 부르면 그것이
/// 곧 새로운 혼잡이고, 무료 공급자의 하루 몫도 한 번에 태운다. 그리고
/// <b>한 건이라도 실패하면 그 바퀴는 거기서 멈춘다</b> — AI 가 지금 못 받는다는
/// 뜻이라 남은 건을 더 두드려 봐야 같은 답이다. 다음 바퀴에 다시 본다.
/// </para>
/// <para>
/// <b>오래된 것은 줍지 않는다.</b> 요약은 「끝났나, 내가 볼 것이 있나」를 읽는
/// 자리라 며칠 지난 건에는 값이 거의 없는데, 그것들까지 훑으면 밀린 목록이
/// 줄지 않아 정작 방금 끝난 건이 뒤로 밀린다.
/// </para>
/// </remarks>
public sealed class AiSummaryCatchUp(
    IConfiguration configuration,
    IServiceScopeFactory scopes,
    ILogger<AiSummaryCatchUp> logger) : BackgroundService
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 몇 분마다 보나. <b>짧게 잡을 이유가 없다</b> — 끝나는 순간의 빠른 길이
    /// 대부분을 이미 처리했고, 여기 오는 것은 그때 AI 가 붐볐던 건이다.
    /// 붐빔은 분 단위로 풀리므로 그보다 잦게 두드리면 헛호출만 는다.
    /// </summary>
    private readonly TimeSpan _period = TimeSpan.FromMinutes(
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpMinutes", defaultValue: 10), 1, 720));

    /// <summary>한 바퀴에 볼 건수.</summary>
    private readonly int _batch =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpBatch", defaultValue: 5), 1, 50);

    /// <summary>며칠 전 것까지 줍나. 0 이면 이 감시자를 돌리지 않는다.</summary>
    private readonly int _withinDays =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpDays", defaultValue: 7), 0, 90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            logger.LogWarning("연결 문자열이 없어 처리 요약 채우기를 돌리지 않습니다.");
            return;
        }

        if (_withinDays == 0)
        {
            logger.LogInformation("AiTasks:SummaryCatchUpDays 가 0 이라 처리 요약 채우기를 끕니다.");
            return;
        }

        logger.LogInformation(
            "처리 요약 채우기 시작 (주기 {Min}분, 한 바퀴 {Batch}건, 최근 {Days}일).",
            _period.TotalMinutes, _batch, _withinDays);

        using var timer = new PeriodicTimer(_period);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // 한 바퀴 실패로 감시자를 죽이지 않는다. 죽으면 그 뒤로 요약이
                // 다시 「영영 안 오는 것」이 되고, 그 사실조차 안 보인다.
                logger.LogError(ex, "처리 요약 채우기 한 바퀴가 실패했습니다.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);

        // **작업의 마지막 실행인지 같이 읽는다.** 요약의 첫 문장으로 작업 제목을
        // 고쳐 주는 기능이 있는데(`AiRunSummaryWriter.EnsureAsync` 의 retitle),
        // 밀린 옛 실행에도 그것을 걸면 **지난 실행의 한 줄이 지금 제목을 덮는다.**
        var rows = (await db.QueryAsync<Pending>("""
            SELECT r.run_key AS RunKey,
                   (r.run_key = t.last_run_key) AS IsLatest
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t ON t.task_key = r.task_key
             WHERE r.finished_at IS NOT NULL
               AND r.finished_at > now() - make_interval(days => @days)
               AND r.summary_text IS NULL
               AND r.result_text IS NOT NULL
               AND btrim(r.result_text) <> ''
             ORDER BY r.finished_at DESC
             LIMIT @batch
            """, new { days = _withinDays, batch = _batch })).ToList();

        if (rows.Count == 0)
        {
            return;
        }

        logger.LogInformation("처리 요약이 빈 실행 {Count}건을 다시 만들어 봅니다.", rows.Count);

        // **범위는 바퀴마다 새로 연다.** 요약 쓰는 이들은 요청 수명(scoped)에
        // 묶여 있는데 여기는 요청이 없다.
        using var scope = scopes.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<AiRunSummaryWriter>();

        var made = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            var summary = await writer.EnsureAsync(row.RunKey, retitle: row.IsLatest, ct);

            if (summary is null)
            {
                // AI 가 지금 못 받는다. **남은 건도 같은 답이다** — 여기서 멈추고
                // 다음 바퀴에 다시 본다.
                logger.LogInformation(
                    "실행 {RunKey} 의 처리 요약을 아직 만들지 못했습니다. 이번 바퀴는 여기서 멈춥니다.",
                    row.RunKey);
                break;
            }

            made++;
        }

        if (made > 0)
        {
            logger.LogInformation("처리 요약 {Count}건을 뒤늦게 채웠습니다.", made);
        }
    }

    /// <summary>요약이 비어 있는 실행 하나.</summary>
    private sealed class Pending
    {
        public long RunKey { get; set; }

        /// <summary>그 작업의 마지막 실행인가. 제목을 고쳐도 되는지가 여기서 갈린다.</summary>
        public bool IsLatest { get; set; }
    }
}
