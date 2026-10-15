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
/// <b>제목도 여기서 붙인다.</b> 요약을 받아 오는 그 한 번의 호출이 짧은 제목까지
/// 함께 들고 온다(<see cref="AiResultSummary.Title"/>). 예전에는 요약이 적힌
/// <i>뒤에</i> 제목을 짓는 호출이 한 번 더 있었고(<see cref="AiTaskTitler"/>),
/// <b>결과 메일이 그 둘을 모두 기다리고 서 있었다.</b> 한 번으로 줄이면 완료에서
/// 메일까지의 거리가 모델 왕복 하나만큼 가까워진다.
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
        catch (OperationCanceledException)
        {
            // **상한에 걸린 것은 고장이 아니다.** 완료 뒤처리가 일부러 거는
            // 상한이고(<c>AiTasks:PostRunWaitSeconds</c>), 그 건은 따라잡기가
            // 다시 집는다. 경고로 올리면 자취 더미가 매번 함께 찍혀 **진짜
            // 실패가 그 사이에 묻힌다.**
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
            // **차수 제목도 여기서 덮는다.** 시킬 때 지시문만 보고 지은 것이
            // 이미 적혀 있는데(<c>AiTurnTitler</c>), 지금 손에 든 것은 **결과까지
            // 보고 지은** 제목이라 더 정확하다. 「무엇을 시켰나」가 「무엇을
            // 했나」로 바뀌는 자리다.
            //
            // <b><c>retitle</c> 과 <c>title_auto</c> 를 보지 않는다.</b> 그 둘이
            // 지키는 것은 <b>건의 제목</b>(사람이 적었을 수 있다)이고, 차수 제목은
            // 사람이 적는 자리가 아예 없다 — 여기서 안 적으면 영영 비어 있다.
            // 모델이 제목 칸을 빠뜨렸으면 널이고, 그때는 적혀 있던 것을 그대로 둔다.
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

            // **사람이 적은 제목은 손대지 않는다**(`title_auto`). 예전에는 이
            // 한 줄에 그 조건이 없어서 **제목을 직접 써 넣은 건까지** 요약의
            // 첫 문장으로 덮였다 — 「AI 작업」 화면에서 제목을 고쳐 두면
            // 다음 실행이 끝나는 순간 되돌아갔다.
            //
            // **도장(`title_run_key`)도 여기서 찍는다.** 제목 짓기
            // (<c>AiTaskTitler</c>)가 그것을 보고 「이 실행은 이미 제목이
            // 붙었다」를 알아 모델을 한 번 더 부르지 않는다.
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
