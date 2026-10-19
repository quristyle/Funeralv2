using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>끝난 실행의 「처리 파일」 목록(<c>ai_task_run.changed_files</c>)을 합쳐서 적는다. 실행기의 git 목록과 모델이 적은 목록을 같이 쓴다.</summary>
public sealed class AiRunFileWriter(
    IConfiguration configuration, AiResultSummarizer summarizer, ILogger<AiRunFileWriter> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>이 실행의 「처리 파일」 목록을 있게 만든다. 이미 합쳐 뒀으면 그대로 두고 돌아온다. 못 하면 false 이고, 던지지 않는다.</summary>
    public async Task<bool> EnsureAsync(long runKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            // 모델이 결과문에 적은 경로에서 대상 뿌리를 떼 내야 「대상 위치 다음부터」가 된다.
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
                // 양쪽 다 빈손이면 도장을 찍지 않는다 — 다음 바퀴에 다시 본다.
                logger.LogInformation(
                    "실행 {RunKey} 의 처리 파일을 아직 채우지 못했습니다 (git 목록도 모델 답도 없습니다).",
                    runKey);
                return false;
            }

            // 경로만 적힌 목록이지만 가리는 손길을 거친다 — 작업공간 경로에 사람 이름이나 토큰이 섞여 들어올 수 있다.
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
            // 상한에 걸린 것은 고장이 아니다 — 완료 뒤처리가 일부러 거는 상한이고, 그 건은 따라잡기가 다시 집는다.
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
