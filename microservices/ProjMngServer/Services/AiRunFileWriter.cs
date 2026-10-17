using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 끝난 실행의 <b>「처리 파일」 목록</b>(<c>ai_task_run.changed_files</c>)을
/// 책임지고 <b>합쳐서</b> 적는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 생겼나 (2026-10-17).</b> 그 칸을 채우는 자리는 실행기 하나뿐이었다
/// (<c>AiTaskRunner.Workspace.ChangedFilesAsync</c>). 실행기가 못 보내면 끝이고,
/// 아무도 다시 채워 주지 않는다 — 운영에서 <b>최근 2주 276건 전부</b>가 그랬다
/// (실행기 바이너리가 그 칸이 생기기 전 것이었다). 화면의 「처리 파일」 탭은
/// 늘 「없음」이었고, 그것이 <b>정말로 안 고친 건</b>인지 <b>값이 안 온 건</b>인지
/// 가릴 길이 없었다.
/// </para>
/// <para>
/// <b>그래서 길을 둘로 둔다.</b> ① 실행기가 끝나자마자 물어 보낸 git 목록이
/// 정본이고, ② 결과문을 모델에게 읽혀 뽑은 목록이 보강이다
/// (<see cref="AiResultSummarizer.ExtractFilesAsync"/>). 둘을 합치는 규칙은
/// <see cref="AiChangedFiles.Merge"/> 에 있다 — <b>git 이 언제나 이기고</b>,
/// git 이 못 본 줄만 「추정」 표를 달고 뒤에 붙는다.
/// </para>
/// <para>
/// <b>두 번 하지 않는다.</b> 합친 자리에 도장을 찍고(<c>files_merged_at</c>)
/// 그 도장이 있으면 곧장 돌아온다. 모델을 한 번 더 부르는 일이고, 두 번째
/// 호출이 첫 번째와 다른 답을 주면 목록이 이유 없이 흔들린다.
/// </para>
/// <para>
/// <b>무슨 일이 있어도 던지지 않는다.</b> 처리 요약과 같은 자리에서 불리는
/// 곁다리라, 이것 때문에 완료 처리나 알림이 흔들리면 본말이 뒤집힌다.
/// </para>
/// </remarks>
public sealed class AiRunFileWriter(
    IConfiguration configuration, AiResultSummarizer summarizer, ILogger<AiRunFileWriter> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 이 실행의 「처리 파일」 목록을 <b>있게 만든다.</b> 이미 합쳐 뒀으면 그대로
    /// 두고 돌아온다. <b>못 하면 false 이고, 던지지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <b>모델이 빈손이어도 도장은 찍는다</b> — 단, 실행기가 보낸 git 목록이
    /// 이미 있을 때만이다. git 목록도 없고 모델도 빈손이면 <b>도장을 안 찍어</b>
    /// 따라잡기가 다시 본다. 그 건이야말로 이 기능이 있는 까닭이라, 한 번
    /// 실패했다고 영영 비워 둘 수 없다.
    /// </remarks>
    public async Task<bool> EnsureAsync(long runKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            // **대상 위치까지 읽는다.** 모델이 결과문에 온 경로를 적어 오면
            // 그 뿌리를 떼 내야 「대상 위치 다음부터」가 된다
            // (<see cref="AiChangedFiles.Normalize"/>).
            var row = await db.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition("""
                SELECT r.instruction     AS Instruction,
                       r.result_text     AS ResultText,
                       r.changed_files   AS ChangedFiles,
                       r.files_merged_at AS MergedAt,
                       g.target_path     AS TargetPath
                  FROM projmng.ai_task_run r
                  JOIN projmng.ai_task t   ON t.task_key   = r.task_key
                  JOIN projmng.ai_target g ON g.target_key = t.target_key
                 WHERE r.run_key = @runKey
                """, new { runKey }, cancellationToken: ct));

            if (row is null || row.MergedAt is not null)
            {
                return row is not null;
            }

            var derived = await summarizer.ExtractFilesAsync(row.Instruction, row.ResultText, ct);

            if (derived is null && string.IsNullOrWhiteSpace(row.ChangedFiles))
            {
                // 양쪽 다 빈손이다. **도장을 찍지 않는다** — 다음 바퀴에 다시 본다.
                logger.LogInformation(
                    "실행 {RunKey} 의 처리 파일을 아직 채우지 못했습니다 (git 목록도 모델 답도 없습니다).",
                    runKey);
                return false;
            }

            // **경로만 적힌 목록이지만 가리는 손길은 거친다.** 작업공간 경로에
            // 사람 이름이나 토큰이 섞여 들어온 적이 있다(`AiRunService.CompleteAsync`
            // 가 실행기 쪽 값에 같은 일을 한다).
            var merged = SecretMask.Apply(
                AiChangedFiles.Merge(row.ChangedFiles, derived, row.TargetPath));

            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE projmng.ai_task_run
                   SET changed_files   = @merged,
                       files_merged_at = now()
                 WHERE run_key = @runKey
                """, new { runKey, merged }, cancellationToken: ct));

            var rows = AiChangedFiles.Parse(merged);

            logger.LogInformation(
                "실행 {RunKey} 의 처리 파일 {Total}건을 적었습니다 (git {Git}건 · 결과문에서 더한 것 {Added}건).",
                runKey, rows.Count, rows.Count(f => !f.Derived), rows.Count(f => f.Derived));

            return true;
        }
        catch (OperationCanceledException)
        {
            // **상한에 걸린 것은 고장이 아니다.** 완료 뒤처리가 일부러 거는
            // 상한이고(`AiTasks:PostRunWaitSeconds`), 그 건은 따라잡기가 다시 집는다.
            logger.LogInformation(
                "실행 {RunKey} 의 처리 파일을 기다리다 상한에 걸렸습니다. 따라잡기가 다시 봅니다.", runKey);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "처리 파일 목록을 합치지 못했습니다 (run {RunKey}).", runKey);
            return false;
        }
    }

    /// <summary>합치는 데 필요한 것만 읽는다.</summary>
    private sealed class FileRow
    {
        public string? Instruction { get; set; }

        public string? ResultText { get; set; }

        public string? ChangedFiles { get; set; }

        public DateTime? MergedAt { get; set; }

        /// <summary>대상 위치 — 경로를 재는 뿌리다.</summary>
        public string? TargetPath { get; set; }
    }
}
