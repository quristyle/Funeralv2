using System.Data;
using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>끝난 실행을 보고 제목을 다시 짓는다. 제목이 비어 있던 건만 AI 가 짧은 제목(20자 내외)을 지어 넣는다.</summary>
/// 보통은 요약과 같은 호출에서 제목이 붙고(AiRunSummaryWriter), 여기는 그 도장이 없는 건만 줍는다. 차수 제목도 같은 걸음에 채운다.
public sealed class AiTaskTitler(
    IConfiguration configuration,
    AiResultSummarizer summarizer,
    ILogger<AiTaskTitler> logger)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>끝난 실행을 보고 제목을 다시 짓는다.</summary>
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

            // 건의 제목은 이 실행이 마지막이고 사람이 쓴 것이 아닐 때만 손댄다(title_auto). 이미 요약과 함께 붙었으면 다시 부르지 않는다.
            // 옛 실행을 주워 부를 때 건의 제목을 고치면 지금 제목이 옛 차수 제목으로 되돌아가므로, 마지막 실행이 아니면 건드리지 않는다.
            var needTask = task.TitleAuto && task.TitleRunKey != runKey && task.IsLatest;

            // 차수 제목은 title_auto 와 무관하게 채운다 — 사람이 쓰는 자리가 없고, 비어 있으면 아무도 못 짓는다.
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

        /// <summary>이 작업의 마지막 실행인가. 거짓이면 건의 제목을 건드리지 않는다.</summary>
        public bool IsLatest { get; set; }
    }
}
