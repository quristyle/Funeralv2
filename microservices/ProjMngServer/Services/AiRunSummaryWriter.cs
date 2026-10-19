using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>끝난 실행의 처리 요약(ai_task_run.summary_text)을 적는다. 알림 설정이 요약을 막지 않도록 알림과 구조로 갈라 둔다.</summary>
/// 이미 적혀 있으면 되읽고, 빈손이면 AiTasks:SummaryAttempts 번까지 다시 부른다. 제목도 같은 호출로 받는다. 던지지 않는다.
public sealed class AiRunSummaryWriter(
    IConfiguration configuration, AiResultSummarizer summarizer, ILogger<AiRunSummaryWriter> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>재시도 상한. 기본 둘 — 메일이 이 결과를 기다리므로 넉넉히 잡으면 결과 메일이 그만큼 늦는다.</summary>
    private readonly int _attempts =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryAttempts", defaultValue: 2), 1, 5);

    /// <summary>다시 부르기 전에 쉬는 시간. 곧바로 다시 부르면 한도에 또 걸린다.</summary>
    private readonly int _retryDelaySeconds =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryRetryDelaySeconds", defaultValue: 3), 0, 60);

    /// <summary>이 실행의 요약을 있게 만든다. 이미 있으면 그것을, 없으면 만들어 적고 돌려준다. 못 만들면 null, 던지지 않는다.</summary>
    /// <param name="runKey">실행 번호.</param>
    /// <param name="retitle">
    /// 요약의 첫 문장으로 <b>작업 제목도 다시 짓나</b>. 「빠른 지시」로 들어온 건은
    /// 제목이 지시문 앞부분이라, 끝난 뒤 한 일로 고쳐 주면 목록에서 찾기 쉽다.
    /// </param>
    public async Task<AiResultSummary?> EnsureAsync(
        long runKey, bool retitle = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            var row = await db.QuerySingleOrDefaultAsync<SummaryRow>("""
                SELECT r.task_key     AS TaskKey,
                       r.instruction  AS Instruction,
                       r.result_text  AS ResultText,
                       r.summary_text AS SummaryText
                  FROM projmng.ai_task_run r
                 WHERE r.run_key = @runKey
                """, new { runKey });

            if (row is null)
            {
                return null;
            }

            if (AiResultSummary.Parse(row.SummaryText) is { } kept)
            {
                return kept;
            }

            // 답이 없으면 정리할 것도 없다. 여기서 끝낸다 — 화면도 요약 칸을
            // 그리지 않고, 기다리지도 않는다(`AiTaskView.AwaitingSummary`).
            if (string.IsNullOrWhiteSpace(row.ResultText))
            {
                return null;
            }

            var summary = await SummarizeAsync(row, runKey, ct);

            if (summary is null)
            {
                logger.LogInformation(
                    "실행 {RunKey} 의 처리 요약을 {Attempts}번 시도했지만 남기지 못했습니다.",
                    runKey, _attempts);
                return null;
            }

            await SaveAsync(db, row, runKey, summary, retitle);

            return summary;
        }
        catch (OperationCanceledException)
        {
            // 상한에 걸린 것은 고장이 아니다 — 완료 뒤처리가 일부러 거는 상한이고 따라잡기가 다시 집는다. 경고로 올리면 진짜 실패가 묻힌다.
            logger.LogInformation(
                "실행 {RunKey} 의 처리 요약을 기다리다 상한에 걸렸습니다. 따라잡기가 다시 봅니다.",
                runKey);
            return null;
        }
        catch (Exception ex)
        {
            // **여기서 실패해도 부르는 쪽은 그대로 간다.** 완료 처리도 메일도
            // 요약이 없다고 멈출 일이 아니다.
            logger.LogWarning(ex, "처리 요약을 만들지 못했습니다 (run {RunKey}).", runKey);
            return null;
        }
    }

    /// <summary>빈손으로 돌아오면 <see cref="_attempts"/> 번까지 다시 부른다.</summary>
    private async Task<AiResultSummary?> SummarizeAsync(
        SummaryRow row, long runKey, CancellationToken ct)
    {
        for (var turn = 1; ; turn++)
        {
            var summary = await summarizer.SummarizeAsync(row.Instruction, row.ResultText, ct);

            if (summary is not null || turn >= _attempts)
            {
                return summary;
            }

            logger.LogInformation(
                "실행 {RunKey} 의 처리 요약을 {Delay}초 뒤에 다시 만들어 봅니다 ({Turn}/{Attempts}).",
                runKey, _retryDelaySeconds, turn, _attempts);

            if (_retryDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_retryDelaySeconds), ct);
            }
        }
    }

    /// <summary>만든 것을 적어 둔다. 적지 못해도 손에 든 요약은 그대로 쓴다.</summary>
    /// 적힌 값은 이어가기 지시문의 문맥으로도 올라가므로 결과문 전문 대신 이 값을 쓴다.
    private async Task SaveAsync(
        NpgsqlConnection db, SummaryRow row, long runKey, AiResultSummary summary, bool retitle)
    {
        try
        {
            // 차수 제목도 여기서 덮는다. 시킬 때 지은 것보다 결과까지 본 것이 정확하다.
            // retitle·title_auto 는 건 제목을 지키는 값이라 여기서는 보지 않는다 — 차수 제목은 사람이 쓰는 자리가 없다.
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task_run
                   SET summary_text = @text,
                       turn_title   = COALESCE(LEFT(@turnTitle, 200), turn_title)
                 WHERE run_key = @runKey
                """, new
            {
                runKey,
                text = summary.ToText(),
                turnTitle = summary.TitleOrHeadline is { Length: > 0 } made ? made : null,
            });

            if (!retitle || summary.TitleOrHeadline is not { Length: > 0 } title)
            {
                return;
            }

            // 사람이 적은 제목은 손대지 않는다(title_auto). title_run_key 도장을 찍어 제목 짓기가 이 실행을 다시 부르지 않게 한다.
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET title         = LEFT(@title, 200),
                       title_run_key = @runKey,
                       mod_dt        = now()
                 WHERE task_key   = @taskKey
                   AND is_deleted = false
                   AND title_auto = true
                """, new { taskKey = row.TaskKey, runKey, title });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "정리된 요약을 저장하지 못했습니다 (run {RunKey}).", runKey);
        }
    }

    /// <summary>요약에 필요한 것만 읽는다.</summary>
    private sealed class SummaryRow
    {
        public long TaskKey { get; set; }

        public string? Instruction { get; set; }

        public string? ResultText { get; set; }

        public string? SummaryText { get; set; }
    }
}
