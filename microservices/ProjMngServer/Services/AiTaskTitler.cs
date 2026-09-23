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
/// 정리된 요약(<c>summary_text</c>)이 있으면 그것의 헤드라인을 읽혀 AI 호출 비용을 아낀다.
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
                       r.summary_text AS SummaryText
                  FROM projmng.ai_task_run r
                  JOIN projmng.ai_task t ON t.task_key = r.task_key
                 WHERE r.run_key = @runKey
                   AND t.is_deleted = false
                """, new { runKey });

            if (task is null || !task.TitleAuto)
            {
                // 사람이 쓴 제목이거나 작업이 없으면 기계가 손대지 않는다.
                return;
            }

            // 정리된 요약이 있으면 그것을 읽혀 값을 아낀다.
            var summary = AiResultSummary.Parse(task.SummaryText);
            var headline = summary?.Headline;

            var newTitle = await summarizer.MakeTitleAsync(
                task.Instruction, task.ResultText, headline, ct);

            if (!string.IsNullOrWhiteSpace(newTitle))
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

                logger.LogInformation(
                    "작업 {TaskKey} 의 제목을 자동 생성했습니다: {Title}", task.TaskKey, newTitle);
            }
            else
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
    }
}
