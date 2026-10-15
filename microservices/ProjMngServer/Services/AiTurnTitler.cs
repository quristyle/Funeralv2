using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// <b>시키는 순간</b> 그 차수의 제목을 짓는다 — <c>ai_task_run.turn_title</c>.
/// </summary>
/// <remarks>
/// <para>
/// 「지시와 결과」 탭은 차수마다 한 묶음을 접어 둔다. 접힌 머리줄이 말하는 것은
/// <b>몇 차인가 · 어떻게 끝났나 · 언제 얼마나 돌았나</b> 셋뿐이라, 세 차수가
/// 쌓이면 <b>어느 것을 펴야 하는지 알 수 없다</b> — 셋 중 무엇도 「무엇을 시킨
/// 차수였나」를 말해 주지 않기 때문이다. 그 한 줄을 여기서 만든다.
/// </para>
/// <para>
/// <b>제목은 두 번 지어진다.</b> 여기가 첫 번째(시킬 때 · 지시문만 보고)이고,
/// 두 번째는 끝났을 때 처리 요약과 <b>같은 한 번의 호출</b>로 와서 이것을 덮는다
/// (<see cref="AiRunSummaryWriter"/>). 뒤엣것이 결과까지 본 제목이라 더 정확하다 —
/// 이쪽은 <b>돌고 있는 동안 머리줄이 비어 있지 않게</b> 하는 값이다.
/// </para>
/// <para>
/// <b>시키는 그 순간에는 실행 줄이 아직 없다.</b> 실행 줄은 실행기가 집어 갈 때
/// 생긴다(<see cref="AiRunService"/> 의 <c>ClaimAsync</c>). 그래서 만든 제목을
/// <c>ai_task.next_turn_title</c> 에 <b>잠깐 얹어 두고</b>, 집어 갈 때 실행 줄로
/// 옮겨 담으면서 그 칸을 비운다. 모델이 집어가기보다 늦게 답하면 그때는
/// <b>방금 생긴 실행 줄에 바로 적는다</b>(아래 <see cref="PrepareAsync"/>).
/// </para>
/// <para>
/// <b>맡기고 가는 일이다.</b> 부르는 쪽(<c>AiTaskService.RequestAsync</c>)은
/// 사람이 누른 단추 뒤에 서 있다 — 모델 왕복 한 번을 거기 붙이면 「시키기」가
/// 그만큼 늦게 끝난다. 제목이 없다고 작업이 못 도는 것도 아니다.
/// </para>
/// <para>
/// <b>무슨 일이 있어도 던지지 않는다.</b> 제목은 곁다리다.
/// </para>
/// </remarks>
public sealed class AiTurnTitler(
    IConfiguration configuration,
    AiResultSummarizer summarizer,
    ILogger<AiTurnTitler> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 이 건의 <b>지금 본문</b>으로 다음 차수의 제목을 지어 얹어 둔다.
    /// </summary>
    /// <remarks>
    /// 요청이 받아들여진 <b>뒤에</b> 불러야 한다 — 상태가 <c>queued</c> 인 것이
    /// 「아직 안 집혀 갔다」의 근거이고, 아래 첫 UPDATE 가 그 값을 조건으로 쓴다.
    /// </remarks>
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

    /// <summary>
    /// 지은 제목을 <b>갈 자리에 적는다</b> — 아직 안 집혀 갔으면 작업에,
    /// 그 사이 집혀 갔으면 방금 생긴 실행 줄에.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>경합은 행 잠금이 가른다.</b> 집어가기는 <c>SELECT … FOR UPDATE OF a</c>
    /// 로 작업 줄을 잠근 채 실행 줄을 만들므로, 그 사이에 들어온 아래 첫 UPDATE 는
    /// 커밋까지 기다렸다가 <c>task_status = 'queued'</c> 가 아닌 것을 보고 0줄을
    /// 돌려준다. 그때는 실행 줄이 이미 있으니 거기에 적는다 — <b>제목이 어느
    /// 쪽으로도 안 가는 틈은 없다.</b>
    /// </para>
    /// <para>
    /// <b>화면 최상단의 제목도 같이 간다</b>(<c>ai_task.title</c>). 「마지막
    /// 차수의 제목이 곧 그 건의 제목」이기 때문이다. 다만 <b>사람이 손으로 적은
    /// 제목은 손대지 않는다</b>(<c>title_auto = false</c>) — 끝난 뒤의 제목 짓기가
    /// 지키는 그 규칙 그대로다.
    /// </para>
    /// </remarks>
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

        // 그 사이에 집혀 갔다(또는 취소됐다). **방금 생긴 실행 줄**에 적는다 —
        // `turn_title IS NULL` 로 막아 두므로 끝난 뒤의 제목(결과까지 본 것)을
        // 덮어쓰지 않는다.
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

/// <summary>
/// 지시문에서 <b>그 차수에 사람이 적은 글</b>만 꺼낸다.
/// </summary>
/// <remarks>
/// <para>
/// 화면 쪽에 같은 일을 하는 것이 있다(<c>AiTaskAsk.Split</c> · <c>AiTaskAlways.Strip</c>).
/// <b>그쪽은 보여 주려고 가르고 이쪽은 제목을 지으려고 가른다</b> — 가르는
/// 자리(머리표)가 같을 뿐 쓰임이 다르고, 모듈이 갈려 있어 코드를 나눠 쓸 수도 없다.
/// </para>
/// <para>
/// <b>왜 가르나.</b> 2차부터의 지시문은 서버가 지난 진행을 앞에 덧대 한 덩어리로
/// 만든 것이다(<c>AiTaskService.BuildContinuation</c>). 통째로 모델에게 주면
/// <b>1차의 이야기가 2차의 제목이 된다</b> — 차수를 가르려고 만든 제목이 차수를
/// 못 가른다.
/// </para>
/// <para>
/// <b>늘 따라붙는 문구도 걷는다.</b> 지시마다 똑같이 뒤에 붙는 네 문단인데
/// (화면의 <c>AiTaskAlways</c> 가 붙인다) 사람이 적은 글보다 긴 일이 흔해서,
/// 안 걷으면 <b>어느 차수든 제목이 「커밋·push 확인」이 된다.</b> 그 문단들은
/// 더해지기만 하고 <b>첫 문단은 바뀐 적이 없으므로</b> 거기서 끊는다 — 통째로
/// 견주면 문구가 늘기 전의 옛 건에서는 하나도 안 걷힌다.
/// </para>
/// </remarks>
internal static class TurnAsk
{
    /// <summary>이어서 지시가 세우는 머리표(<c>AiTaskService.BuildContinuation</c>).</summary>
    private const string TurnMark = "## 이번에 할 일";

    /// <summary>재시도가 세우는 머리표(<c>AiTaskService.RetryAsync</c>).</summary>
    private const string RetryMark = "## 재시도 추가 지시";

    /// <summary>
    /// 늘 따라붙는 문구의 <b>첫 문단 첫 마디</b>. 여기서부터 끝까지가 기계가 붙인 것이다.
    /// </summary>
    private const string AlwaysMark = "작업에 생성되는 파일이나 변경된 내용이 실제 작성되었는지";

    /// <summary>
    /// 사람이 적은 부분만 돌려준다. 가를 것이 없으면 받은 것을 그대로.
    /// </summary>
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
