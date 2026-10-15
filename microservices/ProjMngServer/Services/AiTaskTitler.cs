using System.Data;
using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 끝난 실행을 보고 제목을 다시 짓는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>제목이 비어 있던 건만</b> 작업이 끝난 뒤 AI 가 지시문과 결과를 보고
/// 아주 짧은 제목(20자 내외)을 지어 다시 넣는다. 사람이 적은 제목은 읽지도 않는다.
/// </para>
/// <para>
/// <b>보통의 길에서는 여기까지 오지 않는다.</b> 제목은 처리 요약과 <b>같은 한 번의
/// 모델 호출</b>로 와서 요약을 적는 자리가 함께 붙이고 도장(<c>title_run_key</c>)까지
/// 찍는다(<see cref="AiRunSummaryWriter"/>). 이 자리는 그 도장이 없는 건 —
/// 요약을 못 만들었거나 모델이 제목 칸만 빠뜨린 건 — 의 되받기다.
/// </para>
/// <para>
/// 정리된 요약(<c>summary_text</c>)이 있으면 그것의 헤드라인을 읽혀 AI 호출 비용을 아낀다.
/// </para>
/// <para>
/// <b>차수 제목(<c>ai_task_run.turn_title</c>)도 같은 걸음에 채운다</b>(2026-10-15).
/// 그쪽은 <c>title_auto</c> 를 보지 않는다 — 사람이 적는 자리가 없는 칸이라
/// 비어 있으면 그냥 아무도 못 지은 것이다. 그래서 <b>사람이 제목을 직접 써 둔
/// 건에도 여기까지 온다</b>: 건의 제목은 그대로 두고 차수 제목만 채운다.
/// </para>
/// </remarks>
public sealed class AiTaskTitler(
    IConfiguration configuration,
    AiResultSummarizer summarizer,
    ILogger<AiTaskTitler> logger)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// 끝난 실행을 보고 제목을 다시 짓는다.
    /// </summary>
    public async Task TitleAsync(long runKey, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            // **요약이 상한을 다 썼다**(<c>AiTasks:PostRunWaitSeconds</c> 를
            // 요약과 제목이 나눠 쓴다). 여기서 돌아서지 않고 아래까지 가면
            // 모델을 불러 볼 길이 없는데 **도장만 찍는다** — 그러면 나중에
            // 모델이 살아난 뒤에도 「이 실행은 제목이 붙었다」로 읽힌다.
            return;
        }

        try
        {
            using var db = Open();

            var task = await db.QuerySingleOrDefaultAsync<TaskInfo>("""
                SELECT t.task_key AS TaskKey,
                       t.title AS Title,
                       t.title_auto AS TitleAuto,
                       t.title_run_key AS TitleRunKey,
                       r.instruction AS Instruction,
                       r.result_text AS ResultText,
                       r.summary_text AS SummaryText,
                       r.turn_title AS TurnTitle,
                       (r.run_key = t.last_run_key) AS IsLatest
                  FROM projmng.ai_task_run r
                  JOIN projmng.ai_task t ON t.task_key = r.task_key
                 WHERE r.run_key = @runKey
                   AND t.is_deleted = false
                """, new { runKey });

            if (task is null)
            {
                return;
            }

            // **건의 제목**을 손댈 자리인가. 셋을 본다.
            //
            //   · 사람이 쓴 제목은 읽지도 않는다(`title_auto`).
            //   · 이미 이 실행으로 붙은 제목이면 모델을 또 부르지 않는다 —
            //     요약과 한 번에 받아 적은 길이다(<see cref="AiRunSummaryWriter"/>).
            //     여기서 또 부르면 결과 메일이 그만큼 늦고, 같은 실행에 서로
            //     다른 제목이 두 번 적힌다.
            //   · **이 작업의 마지막 실행이어야 한다.** 건의 제목은 「마지막
            //     차수의 제목」이라, 밀린 옛 실행을 주워 부를 때(감시자의
            //     차수 제목 채우기) 그 한 줄이 **지금 제목을 1차의 것으로
            //     되돌린다.** 요약 채우기가 `retitle: IsLatest` 로 막아 둔 것과
            //     같은 자리이고, 2026-10-15 에 실제로 한 번 밟았다 — 옛 실행부터
            //     훑으니 229건 가운데 173건의 제목이 옛 차수의 것으로 덮였다.
            var needTask = task.TitleAuto && task.TitleRunKey != runKey && task.IsLatest;

            // **차수 제목**을 채울 자리인가. 이쪽은 `title_auto` 를 보지 않는다 —
            // 사람이 적는 자리가 아예 없는 칸이라, 비어 있으면 그냥 아무도 못 지은
            // 것이다(시킬 때의 <see cref="AiTurnTitler"/> 도, 끝난 뒤의 요약도
            // 빈손이었다). 그 칸이 비면 차수 머리줄이 번호와 상태만 말한다.
            var needTurn = string.IsNullOrWhiteSpace(task.TurnTitle);

            if (!needTask && !needTurn)
            {
                return;
            }

            // 정리된 요약이 있으면 그것을 읽혀 값을 아낀다.
            var summary = AiResultSummary.Parse(task.SummaryText);
            var headline = summary?.Headline;

            var newTitle = await summarizer.MakeTitleAsync(
                task.Instruction, task.ResultText, headline, ct);

            if (!string.IsNullOrWhiteSpace(newTitle))
            {
                if (needTurn)
                {
                    await db.ExecuteAsync("""
                        UPDATE projmng.ai_task_run
                           SET turn_title = LEFT(@newTitle, 200)
                         WHERE run_key = @runKey
                           AND turn_title IS NULL
                        """, new { runKey, newTitle });
                }

                if (needTask)
                {
                    await db.ExecuteAsync("""
                        UPDATE projmng.ai_task
                           SET title = @newTitle,
                               title_run_key = @runKey,
                               mod_dt = now()
                         WHERE task_key = @TaskKey
                           AND is_deleted = false
                           AND title_auto = true
                        """, new { TaskKey = task.TaskKey, newTitle, runKey });
                }

                logger.LogInformation(
                    "작업 {TaskKey} 의 제목을 자동 생성했습니다: {Title}", task.TaskKey, newTitle);
            }
            else if (needTask)
            {
                // 제목 생성에 실패했더라도 도장은 찍어 준다 — 안 찍으면 화면이 오지 않을 제목을 영원히 기다린다.
                await db.ExecuteAsync("""
                    UPDATE projmng.ai_task
                       SET title_run_key = @runKey,
                           mod_dt = now()
                     WHERE task_key = @TaskKey
                       AND is_deleted = false
                       AND title_auto = true
                    """, new { TaskKey = task.TaskKey, runKey });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "실행 {RunKey} 의 제목 자동 생성 중 오류가 발생했습니다.", runKey);
        }
    }

    private sealed class TaskInfo
    {
        public long TaskKey { get; set; }
        public string? Title { get; set; }
        public bool TitleAuto { get; set; }
        public long? TitleRunKey { get; set; }
        public string? Instruction { get; set; }
        public string? ResultText { get; set; }
        public string? SummaryText { get; set; }

        /// <summary>이 실행의 차수 제목. 비어 있으면 아직 아무도 못 지었다.</summary>
        public string? TurnTitle { get; set; }

        /// <summary>
        /// 이것이 그 작업의 <b>마지막 실행</b>인가. 거짓이면 건의 제목에 손대지
        /// 않는다 — 건의 제목은 마지막 차수의 것이다.
        /// </summary>
        public bool IsLatest { get; set; }
    }
}
