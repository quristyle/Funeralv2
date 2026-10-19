using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>시키는 순간 그 차수의 제목을 짓는다(ai_task_run.turn_title). 접힌 머리줄이 「무엇을 시킨 차수인가」를 말하게 한다.</summary>
/// 제목은 두 번 지어진다 — 여기가 지시문만 보고 짓는 첫 번째이고, 끝난 뒤 요약과 같은 호출이 덮는 것이 두 번째다. 실행 줄은 집어갈 때 생기므로 next_turn_title 에 잠깐 얹어 둔다.
public sealed class AiTurnTitler(
    IConfiguration configuration,
    AiResultSummarizer summarizer,
    ILogger<AiTurnTitler> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>이 건의 지금 본문으로 다음 차수의 제목을 지어 얹어 둔다.</summary>
    /// 요청이 받아들여진 뒤에 불러야 한다 — 상태 queued 가 아직 안 집혀 갔다는 근거를 첫 UPDATE 가 조건으로 쓴다.
    public async Task PrepareAsync(long taskKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            var contents = await db.ExecuteScalarAsync<string?>("""
                SELECT contents FROM projmng.ai_task
                 WHERE task_key = @taskKey AND is_deleted = false
                """, new { taskKey });

            var title = await summarizer.MakeTurnTitleAsync(TurnAsk.Human(contents), ct);

            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            await ApplyAsync(db, taskKey, title!);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "작업 {TaskKey} 의 차수 제목을 짓지 못했습니다.", taskKey);
        }
    }

    /// <summary>지은 제목을 갈 자리에 적는다 — 아직 안 집혀 갔으면 작업에, 집혀 갔으면 방금 생긴 실행 줄에.</summary>
    /// 경합은 행 잠금(FOR UPDATE)이 가른다. 사람이 손으로 쓴 작업 제목(title_auto=false)은 손대지 않는다.
    private async Task ApplyAsync(NpgsqlConnection db, long taskKey, string title)
    {
        var parked = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET next_turn_title = LEFT(@title, 200),
                   title = CASE WHEN title_auto THEN LEFT(@title, 200) ELSE title END,
                   mod_dt = now()
             WHERE task_key   = @taskKey
               AND is_deleted = false
               AND task_status = 'queued'
            """, new { taskKey, title });

        if (parked > 0)
        {
            logger.LogInformation("작업 {TaskKey} 의 다음 차수 제목을 지었습니다: {Title}", taskKey, title);
            return;
        }

        // 그 사이 집혀 갔다. 방금 생긴 실행 줄에 적는다 — turn_title IS NULL 로 막아 끝난 뒤의 제목을 덮지 않는다.
        var written = await db.ExecuteAsync("""
            UPDATE projmng.ai_task_run
               SET turn_title = LEFT(@title, 200)
             WHERE run_key = ( SELECT MAX(run_key) FROM projmng.ai_task_run
                                WHERE task_key = @taskKey )
               AND turn_title IS NULL
            """, new { taskKey, title });

        if (written > 0)
        {
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET title  = LEFT(@title, 200),
                       mod_dt = now()
                 WHERE task_key   = @taskKey
                   AND is_deleted = false
                   AND title_auto = true
                """, new { taskKey, title });

            logger.LogInformation(
                "작업 {TaskKey} 가 먼저 집혀 가, 지은 제목을 실행 줄에 적었습니다: {Title}", taskKey, title);
        }
    }
}

/// <summary>지시문에서 그 차수에 사람이 적은 글만 꺼낸다.</summary>
/// 서버가 덧댄 머리표와 늘 따라붙는 문구를 걷는다 — 통째로 주면 1차의 이야기가 2차의 제목이 된다.
internal static class TurnAsk
{
    /// <summary>이어서 지시가 세우는 머리표(<c>AiTaskService.BuildContinuation</c>).</summary>
    private const string TurnMark = "## 이번에 할 일";

    /// <summary>재시도가 세우는 머리표(<c>AiTaskService.RetryAsync</c>).</summary>
    private const string RetryMark = "## 재시도 추가 지시";

    /// <summary>늘 따라붙는 문구의 <b>첫 문단 첫 마디</b>. 여기서부터 끝까지가 기계가 붙인 것이다.</summary>
    private const string AlwaysMark = "작업에 생성되는 파일이나 변경된 내용이 실제 작성되었는지";

    /// <summary>사람이 적은 부분만 돌려준다. 가를 것이 없으면 받은 것을 그대로.</summary>
    public static string? Human(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return instruction;
        }

        var lines = instruction.Replace("\r\n", "\n").Split('\n');
        var at = -1;

        // **마지막 머리표를 집는다.** 이어서 지시한 건을 다시 재시도하면 둘이
        // 한 글에 함께 있고, 그때 사람이 마지막에 적은 것은 뒤엣것이다.
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() is TurnMark or RetryMark)
            {
                at = i;
            }
        }

        var ask = at < 0 ? instruction.Trim() : string.Join('\n', lines.Skip(at + 1)).Trim();

        // 머리표만 있고 뒤가 비었다. 가르면 줄 글이 통째로 사라지므로 그대로 둔다.
        if (ask.Length == 0)
        {
            ask = instruction.Trim();
        }

        var always = ask.IndexOf(AlwaysMark, StringComparison.Ordinal);

        if (always > 0)
        {
            var left = ask[..always].Trim();

            // 문구밖에 없는 글은 그대로 둔다 — 걷고 나면 지어 줄 것이 없다.
            if (left.Length > 0)
            {
                ask = left;
            }
        }

        return ask;
    }
}
