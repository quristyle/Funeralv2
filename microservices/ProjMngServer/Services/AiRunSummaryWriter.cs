using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 끝난 실행의 <b>처리 요약</b>(<c>ai_task_run.summary_text</c>)을 책임지고 적는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 알림에서 떼어 냈나.</b> 요약을 쓰는 자리는 원래 결과 메일을 만드는
/// 쪽(<see cref="AiTaskNotifier"/>)이었다. 메일 본문에 실을 값이었으니 거기서
/// 만드는 것이 자연스러웠는데, 그러면 <b>요약의 수명이 알림 설정에 매달린다</b> —
/// 메일도 앱푸시도 끄고 시킨 건은 화면의 「처리 요약」 칸이 결과를 읽는 유일한
/// 자리인데, 하필 그 사람에게만 요약이 없다. 한 번은 관문 순서를 바꿔 고쳤지만
/// (2026-09-19) 그것은 주석으로 지킨 약속이라, 알림 쪽을 손대는 다음 사람이
/// 관문을 하나 더 얹으면 조용히 되돌아간다. <b>지금은 구조로 갈라 둔다</b> —
/// 요약은 완료 처리(<see cref="AiRunService"/>)가 부르고, 알림은 적힌 것을 읽는다.
/// </para>
/// <para>
/// <b>두 번 만들지 않는다.</b> 이미 적혀 있으면 그것을 되읽어 돌려준다
/// (<see cref="AiResultSummary.Parse"/>). 모델을 한 번 더 부르는 일이고, 그
/// 값이 메일과 화면에서 서로 다른 말을 하기 시작하면 어느 쪽이 맞는지 가릴
/// 방법이 없다.
/// </para>
/// <para>
/// <b>한 번 실패했다고 포기하지 않는다.</b> AI 호출은 30초에서 끊기고, 바쁘거나
/// 한도에 걸리면 빈손으로 돌아온다. 예전에는 그 한 번이 전부여서
/// <c>summary_text</c> 가 영영 비었다 — 메일을 켠 사람은 옛 방식(결과문 앞 몇 줄)
/// 으로라도 답을 받으니 눈치채지 못했고, 알림을 끈 사람만 빈칸을 봤다.
/// 그래서 <c>AiTasks:SummaryAttempts</c> 번까지 다시 부른다.
/// </para>
/// <para>
/// <b>무슨 일이 있어도 던지지 않는다.</b> 요약은 곁다리다 — 이것 때문에 완료
/// 처리가 흔들리거나 메일이 빠지면 본말이 뒤집힌다.
/// </para>
/// </remarks>
public sealed class AiRunSummaryWriter(
    IConfiguration configuration, AiResultSummarizer summarizer, ILogger<AiRunSummaryWriter> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 몇 번까지 불러 보나. <b>기본 둘이다</b> — 메일이 이 결과를 기다리므로
    /// (완료 → 요약 → 메일 순서), 넉넉히 잡으면 AI 가 죽어 있는 동안 결과
    /// 메일이 그만큼 늦게 나간다. 「빨리 오지 않는 결과 메일은 뜻이 없다」와
    /// 「요약은 항상 있어야 한다」 사이의 타협점이다.
    /// </summary>
    private readonly int _attempts =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryAttempts", defaultValue: 2), 1, 5);

    /// <summary>다시 부르기 전에 쉬는 시간. 곧바로 다시 부르면 한도에 또 걸린다.</summary>
    private readonly int _retryDelaySeconds =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryRetryDelaySeconds", defaultValue: 3), 0, 60);

    /// <summary>
    /// 이 실행의 요약을 <b>있게 만든다.</b> 이미 있으면 그것을, 없으면 만들어
    /// 적고 돌려준다. <b>못 만들면 null 이고, 던지지 않는다.</b>
    /// </summary>
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
        catch (Exception ex)
        {
            // **여기서 실패해도 부르는 쪽은 그대로 간다.** 완료 처리도 메일도
            // 요약이 없다고 멈출 일이 아니다.
            logger.LogWarning(ex, "처리 요약을 만들지 못했습니다 (run {RunKey}).", runKey);
            return null;
        }
    }

    /// <summary>
    /// 빈손으로 돌아오면 <see cref="_attempts"/> 번까지 다시 부른다.
    /// </summary>
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

    /// <summary>
    /// 만든 것을 적어 둔다. <b>적지 못해도 손에 든 요약은 그대로 쓴다</b> —
    /// 못 적어 둔 것은 다음에 한 번 더 부르면 되는 일이다.
    /// </summary>
    /// <remarks>
    /// 적어 두는 값이 화면에서만 쓰이는 것이 아니다. 이어가기 지시문이 이 칸을
    /// 다음 실행의 문맥으로 올려보낸다(<c>AiTaskService.BuildContinuation</c>) —
    /// 예전에는 결과문 전문을 1500자에서 자른 것이 올라갔다.
    /// </remarks>
    private async Task SaveAsync(
        NpgsqlConnection db, SummaryRow row, long runKey, AiResultSummary summary, bool retitle)
    {
        try
        {
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task_run SET summary_text = @text WHERE run_key = @runKey
                """, new { runKey, text = summary.ToText() });

            if (retitle && !string.IsNullOrWhiteSpace(summary.Headline))
            {
                await db.ExecuteAsync("""
                    UPDATE projmng.ai_task SET title = @title WHERE task_key = @taskKey
                    """, new { taskKey = row.TaskKey, title = summary.Headline });
            }
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
