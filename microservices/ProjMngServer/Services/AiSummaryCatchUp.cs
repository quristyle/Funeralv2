using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// <b>비어 있는 「처리 요약」을 뒤늦게 채운다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 생겼나 (2026-09-21).</b> 요약을 만들 기회는 실행이 끝나는 그 한 번뿐이었다
/// (<see cref="AiRunService"/> 의 <c>SummarizeThenNotifyAsync</c>). 그 한 번은
/// 결과 메일이 기다리는 자리라 오래 붙잡을 수 없어서 두 번만 시도하고 3초 쉰다 —
/// <b>AI 가 그 30여 초 동안 붐비면 그 건의 요약은 영영 없다.</b> 아무도 다시
/// 만들어 주지 않고, 화면은 요약 칸을 통째로 빼고 그린다. 실제로 사흘치 실행의
/// 절반가량이 그렇게 비어 있었다(95건).
/// </para>
/// <para>
/// <b>그래서 자리를 나눈다.</b> 끝나는 순간의 한 번은 <i>빠른 길</i>로 남겨
/// 메일을 늦추지 않고, 거기서 못 만든 것은 이 감시자가 주워 간다. 요약을 쓰는
/// 일은 <see cref="AiRunSummaryWriter"/> 한 곳이 그대로 맡으므로 —
/// 이미 적혀 있으면 되읽고 끝낸다 — 두 길이 서로 다른 요약을 만들 일은 없다.
/// </para>
/// <para>
/// <b>한 바퀴에 몇 건만 본다.</b> 밀린 것이 수십 건일 때 한꺼번에 부르면 그것이
/// 곧 새로운 혼잡이고, 무료 공급자의 하루 몫도 한 번에 태운다. 그리고
/// <b>한 건이라도 실패하면 그 바퀴는 거기서 멈춘다</b> — AI 가 지금 못 받는다는
/// 뜻이라 남은 건을 더 두드려 봐야 같은 답이다. 다음 바퀴에 다시 본다.
/// </para>
/// <para>
/// <b>오래된 것은 줍지 않는다.</b> 요약은 「끝났나, 내가 볼 것이 있나」를 읽는
/// 자리라 며칠 지난 건에는 값이 거의 없는데, 그것들까지 훑으면 밀린 목록이
/// 줄지 않아 정작 방금 끝난 건이 뒤로 밀린다.
/// </para>
/// <para>
/// <b>미뤄 둔 결과 메일도 여기서 보낸다 (2026-10-14).</b> 이제 결과 메일은
/// 요약이 손에 들어온 뒤에만 나간다(<c>AiRunService.SummarizeThenNotifyAsync</c>
/// → <c>deferUntilSummary</c>). 그러므로 <b>여기서 요약을 채우는 일이 곧 밀린
/// 메일을 풀어 주는 일</b>이다 — 채운 바로 그 자리에서 보낸다. 예전에는 요약
/// 없는 메일이 먼저 나가고 요약은 뒤늦게 화면에만 생겨서, 받는 사람 눈에
/// <b>「요약이 만들어지기 전에 메일이 온다」</b>로 보였다.
/// </para>
/// <para>
/// <b>영영 미루지는 않는다.</b> 모델이 며칠째 죽어 있으면 요약도 메일도 안 가는데,
/// 그 둘 중 메일은 반드시 가야 한다. 끝난 지 <c>AiTasks:MailDeferMinutes</c> 분이
/// 지나도록 안 나간 건은 <b>요약 없이 그냥 보낸다</b>(그때 메일의 「AI 의 답」 칸은
/// 옛 방식대로 결과문 앞 몇 줄로 채워진다).
/// </para>
/// </remarks>
public sealed class AiSummaryCatchUp(
    IConfiguration configuration,
    IServiceScopeFactory scopes,
    ILogger<AiSummaryCatchUp> logger) : BackgroundService
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 몇 분마다 보나.
    /// </summary>
    /// <remarks>
    /// <b>열 분이었다. 미룬 메일까지 이 바퀴가 보내게 되면서 둘로 내렸다
    /// (2026-10-14).</b> 이 주기는 요약을 다시 만들어 보는 간격인 동시에
    /// <b>미뤄 둔 결과 메일을 집어 가는 간격</b>이다 — 미룸 상한
    /// (<see cref="_mailDeferMinutes"/>, 기본 5분)보다 성기면 「5분 뒤에 보낸다」가
    /// 실제로는 15분이 된다. 상한의 절반 아래로 두는 것이 규칙이다.
    /// <para>
    /// 헛호출 걱정은 그대로 작지만 — 요약이 빈 건이 없으면 질의 한 번으로
    /// 끝나고, 모델이 죽어 있으면 첫 실패에서 바퀴를 접는다.
    /// </para>
    /// </remarks>
    private readonly TimeSpan _period = TimeSpan.FromMinutes(
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpMinutes", defaultValue: 2), 1, 720));

    /// <summary>한 바퀴에 볼 건수.</summary>
    private readonly int _batch =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpBatch", defaultValue: 5), 1, 50);

    /// <summary>며칠 전 것까지 줍나. 0 이면 이 감시자를 돌리지 않는다.</summary>
    private readonly int _withinDays =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpDays", defaultValue: 7), 0, 90);

    /// <summary>
    /// 결과 메일을 <b>요약 없이라도 보내기까지</b> 기다리는 시간(분).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>서른 분이었다. 2026-10-14 에 다섯으로 내렸다.</b> 그 값은 「붐비면
    /// 10~20분은 흔하다」는 바깥 공급자를 전제로 한 것인데, 지금 요약을 만드는
    /// 것은 같은 장비의 LLM 서버이고 실측이 1.0~1.6초다. 즉 여기까지 오는 건은
    /// 「붐벼서 늦는 건」이 아니라 <b>모델이 아예 안 받는 건</b>이고, 그런 건을
    /// 서른 분 붙들고 있어 봐야 요약이 생기지 않는다 — 결과 메일만 늦는다.
    /// </para>
    /// <para>
    /// 완료 직후에 이미 한 번 기다려 봤다는 것도 셈에 넣는다
    /// (<c>AiTasks:PostRunWaitSeconds</c>, 기본 30초). 이 다섯 분은 <b>그
    /// 30초로도 안 된 건에 주는 두 번째 기회</b>다.
    /// </para>
    /// <para>
    /// 바닥은 감시자 주기(<see cref="_period"/>)다 — 그보다 짧게 잡으면 바퀴가
    /// 한 번 돌기도 전에 상한이 지나 「미룬다」는 말이 무의미해진다.
    /// </para>
    /// </remarks>
    private readonly int _mailDeferMinutes =
        Math.Clamp(configuration.GetValue("AiTasks:MailDeferMinutes", defaultValue: 5), 1, 1440);

    /// <summary>
    /// 밀린 메일을 며칠 전 것까지 보나. <b>요약 쪽 창(<see cref="_withinDays"/>)보다
    /// 좁아지지 않게</b> 이레를 바닥으로 깐다 — 요약 채우기를 꺼 둔 장비에서도
    /// 결과 메일은 나가야 한다. 그보다 오래된 건은 메일로 받을 값이 없다.
    /// </summary>
    private readonly int _mailWithinDays = Math.Max(
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpDays", defaultValue: 7), 0, 90), 7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            logger.LogWarning("연결 문자열이 없어 처리 요약 채우기를 돌리지 않습니다.");
            return;
        }

        if (_withinDays == 0)
        {
            // **감시자를 멈추지는 않는다.** 이 값은 「요약을 뒤늦게 만드나」의
            // 손잡이인데, 여기서 돌아서면 **미뤄 둔 결과 메일까지 영영 안 나간다** —
            // 요약을 안 만들기로 한 것과 메일을 안 보내기로 한 것은 다른 결정이다.
            logger.LogInformation(
                "AiTasks:SummaryCatchUpDays 가 0 이라 처리 요약 채우기는 끄고, 미뤄진 결과 메일만 보냅니다.");
        }

        logger.LogInformation(
            "처리 요약 채우기 시작 (주기 {Min}분, 한 바퀴 {Batch}건, 최근 {Days}일, 메일 미룸 상한 {Defer}분).",
            _period.TotalMinutes, _batch, _withinDays, _mailDeferMinutes);

        using var timer = new PeriodicTimer(_period);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // 한 바퀴 실패로 감시자를 죽이지 않는다. 죽으면 그 뒤로 요약이
                // 다시 「영영 안 오는 것」이 되고, 그 사실조차 안 보인다.
                logger.LogError(ex, "처리 요약 채우기 한 바퀴가 실패했습니다.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);

        // **범위는 바퀴마다 새로 연다.** 요약 쓰는 이도 알림 보내는 이도 요청
        // 수명(scoped)에 묶여 있는데 여기는 요청이 없다.
        using var scope = scopes.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<AiRunSummaryWriter>();
        var notifier = scope.ServiceProvider.GetRequiredService<AiTaskNotifier>();

        await FillSummariesAsync(db, writer, notifier, ct);
        await SendOverdueMailsAsync(db, notifier, ct);
    }

    /// <summary>
    /// 요약이 빈 실행을 몇 건 주워 채우고, <b>채운 자리에서 밀린 메일을 보낸다.</b>
    /// </summary>
    private async Task FillSummariesAsync(
        NpgsqlConnection db, AiRunSummaryWriter writer, AiTaskNotifier notifier, CancellationToken ct)
    {
        if (_withinDays == 0)
        {
            return;
        }

        // **작업의 마지막 실행인지 같이 읽는다.** 요약의 첫 문장으로 작업 제목을
        // 고쳐 주는 기능이 있는데(`AiRunSummaryWriter.EnsureAsync` 의 retitle),
        // 밀린 옛 실행에도 그것을 걸면 **지난 실행의 한 줄이 지금 제목을 덮는다.**
        var rows = (await db.QueryAsync<Pending>(new CommandDefinition("""
            SELECT r.run_key AS RunKey,
                   (r.run_key = t.last_run_key) AS IsLatest
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t ON t.task_key = r.task_key
             WHERE r.finished_at IS NOT NULL
               AND r.finished_at > now() - make_interval(days => @days)
               AND r.summary_text IS NULL
               AND r.result_text IS NOT NULL
               AND btrim(r.result_text) <> ''
             ORDER BY r.finished_at DESC
             LIMIT @batch
            """, new { days = _withinDays, batch = _batch }, cancellationToken: ct))).ToList();

        if (rows.Count == 0)
        {
            return;
        }

        logger.LogInformation("처리 요약이 빈 실행 {Count}건을 다시 만들어 봅니다.", rows.Count);

        var made = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            var summary = await writer.EnsureAsync(row.RunKey, retitle: row.IsLatest, ct);

            if (summary is null)
            {
                // AI 가 지금 못 받는다. **남은 건도 같은 답이다** — 여기서 멈추고
                // 다음 바퀴에 다시 본다.
                logger.LogInformation(
                    "실행 {RunKey} 의 처리 요약을 아직 만들지 못했습니다. 이번 바퀴는 여기서 멈춥니다.",
                    row.RunKey);
                break;
            }

            made++;

            // **요약이 생겼으니 미뤄 둔 메일을 지금 보낸다.** 이미 나간 건이면
            // 자리잡기에서 걸러진다(<c>AiTaskNotifier.StampMailAsync</c>).
            await notifier.SendMailAsync(row.RunKey, deferUntilSummary: false, ct);
        }

        if (made > 0)
        {
            logger.LogInformation("처리 요약 {Count}건을 뒤늦게 채웠습니다.", made);
        }
    }

    /// <summary>
    /// <b>너무 오래 미뤄진 결과 메일을 요약 없이 보낸다.</b>
    /// </summary>
    /// <remarks>
    /// 여기까지 온 건은 셋 중 하나다 — 모델이 계속 빈손이거나, 요약을 만들 글이
    /// 없거나(결과문이 비었다), 보내다 실패했거나. <b>어느 쪽이든 메일은 가야
    /// 한다.</b> 미룬다는 약속은 「요약을 기다린다」이지 「안 보낸다」가 아니다.
    /// <para>
    /// 이 칸이 생기기 전의 실행은 되메움으로 전부 찍혀 있다(2026-10-14 의
    /// <c>deploy/sql/projmng-ai-mail-sent-at-2026-10-14.sql</c>). 그 한 줄이
    /// 없으면 여기가 <b>지난 모든 건의 메일을 한꺼번에 쏜다.</b>
    /// </para>
    /// </remarks>
    private async Task SendOverdueMailsAsync(
        NpgsqlConnection db, AiTaskNotifier notifier, CancellationToken ct)
    {
        var runKeys = (await db.QueryAsync<long>(new CommandDefinition("""
            SELECT r.run_key
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t ON t.task_key = r.task_key
             WHERE r.mail_sent_at IS NULL
               AND r.finished_at IS NOT NULL
               AND r.finished_at < now() - make_interval(mins => @mins)
               AND r.finished_at > now() - make_interval(days => @days)
             ORDER BY r.finished_at
             LIMIT @batch
            """,
            new { mins = _mailDeferMinutes, days = _mailWithinDays, batch = _batch },
            cancellationToken: ct))).ToList();

        if (runKeys.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "{Mins}분 넘게 미뤄진 결과 메일 {Count}건을 요약 없이 보냅니다.",
            _mailDeferMinutes, runKeys.Count);

        foreach (var runKey in runKeys)
        {
            ct.ThrowIfCancellationRequested();
            await notifier.SendMailAsync(runKey, deferUntilSummary: false, ct);
        }
    }

    /// <summary>요약이 비어 있는 실행 하나.</summary>
    private sealed class Pending
    {
        public long RunKey { get; set; }

        /// <summary>그 작업의 마지막 실행인가. 제목을 고쳐도 되는지가 여기서 갈린다.</summary>
        public bool IsLatest { get; set; }
    }
}
