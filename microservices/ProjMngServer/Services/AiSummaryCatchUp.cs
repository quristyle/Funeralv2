using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>비어 있는 처리 요약과 차수 제목을 뒤늦게 채우고, 미뤄 둔 결과 메일을 보낸다.</summary>
/// 요약은 완료 순간 한 번만 시도해 못 만들면 영영 비므로 이 감시자가 줍는다. 한 바퀴에 몇 건만 보고, 한 건 실패하면 그 바퀴는 멈춘다.
public sealed class AiSummaryCatchUp(
    IConfiguration configuration,
    IServiceScopeFactory scopes,
    ILogger<AiSummaryCatchUp> logger) : BackgroundService
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>몇 분마다 도는가. 미룬 메일을 집어 가는 간격이라 미룸 상한의 절반 아래여야 한다.</summary>
    private readonly TimeSpan _period = TimeSpan.FromMinutes(
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpMinutes", defaultValue: 2), 1, 720));

    /// <summary>한 바퀴에 볼 건수.</summary>
    private readonly int _batch =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpBatch", defaultValue: 5), 1, 50);

    /// <summary>며칠 전 것까지 줍나. 0 이면 이 감시자를 돌리지 않는다.</summary>
    private readonly int _withinDays =
        Math.Clamp(configuration.GetValue("AiTasks:SummaryCatchUpDays", defaultValue: 7), 0, 90);

    /// <summary>결과 메일을 요약 없이라도 보내기까지 기다리는 시간(분).</summary>
    /// 요약 LLM 은 같은 장비라 여기까지 온 건은 붐벼서 늦은 것이 아니라 모델이 안 받는 것이다. 감시자 주기보다 짧으면 상한이 무의미해진다.
    private readonly int _mailDeferMinutes =
        Math.Clamp(configuration.GetValue("AiTasks:MailDeferMinutes", defaultValue: 5), 1, 1440);

    /// <summary>밀린 메일을 며칠 전 것까지 본다. 요약 창보다 좁아지지 않게 이레를 바닥으로 깐다.</summary>
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

        var tried = await FillSummariesAsync(db, writer, notifier, ct);

        await SendOverdueMailsAsync(db, writer, notifier, tried, ct);

        await FillTurnTitlesAsync(db, scope.ServiceProvider.GetRequiredService<AiTaskTitler>(), ct);

        await FillChangedFilesAsync(db, scope.ServiceProvider.GetRequiredService<AiRunFileWriter>(), ct);
    }

    /// <summary>changed_files 가 비어 있는 실행을 몇 건 주워 채운다.</summary>
    /// 결과문이 있는 건만 본다 — 결과문이 없으면 모델을 불러도 같은 빈손이다.
    private async Task FillChangedFilesAsync(
        NpgsqlConnection db, AiRunFileWriter writer, CancellationToken ct)
    {
        if (_withinDays == 0)
        {
            return;
        }

        var keys = (await db.QueryAsync<long>(new CommandDefinition("""
            SELECT r.run_key
              FROM projmng.ai_task_run r
             WHERE r.finished_at IS NOT NULL
               AND r.finished_at > now() - make_interval(days => @days)
               AND r.files_merged_at IS NULL
               AND r.result_text IS NOT NULL
               AND btrim(r.result_text) <> ''
             ORDER BY r.finished_at DESC
             LIMIT @batch
            """, new { days = _withinDays, batch = _batch }, cancellationToken: ct))).ToList();

        if (keys.Count == 0)
        {
            return;
        }

        logger.LogInformation("처리 파일을 안 합친 실행 {Count}건을 채워 봅니다.", keys.Count);

        foreach (var runKey in keys)
        {
            ct.ThrowIfCancellationRequested();

            // **던지지 않는다**(그쪽이 통째로 감싸고 있다).
            if (!await writer.EnsureAsync(runKey, ct))
            {
                logger.LogInformation(
                    "실행 {RunKey} 의 처리 파일을 아직 채우지 못했습니다. 이번 바퀴는 여기서 멈춥니다.",
                    runKey);
                break;
            }
        }
    }

    /// <summary>turn_title 이 빈 실행을 몇 건 주워 채운다. 요약의 첫 문장을 읽혀 짓는다.</summary>
    /// 한 건 실패로 멈추지 않는다 — 제목 짓기를 끈 장비는 모든 건이 빈손이라 그것은 혼잡이 아니라 설정이다. 바퀴 상한은 _batch 로 따로 둔다.
    private async Task FillTurnTitlesAsync(
        NpgsqlConnection db, AiTaskTitler titler, CancellationToken ct)
    {
        if (_withinDays == 0)
        {
            return;
        }

        var keys = (await db.QueryAsync<long>(new CommandDefinition("""
            SELECT r.run_key
              FROM projmng.ai_task_run r
             WHERE r.finished_at IS NOT NULL
               AND r.finished_at > now() - make_interval(days => @days)
               AND r.turn_title IS NULL
             ORDER BY r.finished_at DESC
             LIMIT @batch
            """, new { days = _withinDays, batch = _batch }, cancellationToken: ct))).ToList();

        if (keys.Count == 0)
        {
            return;
        }

        logger.LogInformation("차수 제목이 빈 실행 {Count}건을 지어 봅니다.", keys.Count);

        foreach (var runKey in keys)
        {
            ct.ThrowIfCancellationRequested();

            // **던지지 않는다**(그쪽이 통째로 감싸고 있다). 못 지으면 다음
            // 바퀴에 같은 건을 다시 본다.
            await titler.TitleAsync(runKey, ct);
        }
    }

    /// <summary>요약이 빈 실행을 몇 건 주워 채우고, 채운 자리에서 밀린 메일을 보낸다.</summary>
    /// <returns>이번 바퀴에 요약을 만들어 본 실행들. 밀린 메일 보내기가 같은 건을 또 두드리지 않게 넘긴다.</returns>
    private async Task<HashSet<long>> FillSummariesAsync(
        NpgsqlConnection db, AiRunSummaryWriter writer, AiTaskNotifier notifier, CancellationToken ct)
    {
        var tried = new HashSet<long>();

        if (_withinDays == 0)
        {
            return tried;
        }

        // **작업의 마지막 실행인지 같이 읽는다.** 요약의 첫 문장으로 작업 제목을
        // 고쳐 주는 기능이 있는데(`AiRunSummaryWriter.EnsureAsync` 의 retitle),
        // 밀린 실행에도 그것을 걸면 지난 실행의 한 줄이 지금 제목을 덮는다.
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
            return tried;
        }

        logger.LogInformation("처리 요약이 빈 실행 {Count}건을 다시 만들어 봅니다.", rows.Count);

        var made = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            tried.Add(row.RunKey);

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

        return tried;
    }

    /// <summary>너무 오래 미뤄진 결과 메일을 요약 없이 보낸다.</summary>
    /// 포기 전에 한 번 더 만든다(이번 바퀴에 두드린 건은 건너뛴다). 되메움이 없는 실행은 지난 건 메일이 쏟아지므로 mail_sent_at 되메움이 먼저다.
    private async Task SendOverdueMailsAsync(
        NpgsqlConnection db,
        AiRunSummaryWriter writer,
        AiTaskNotifier notifier,
        HashSet<long> tried,
        CancellationToken ct)
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

            // 마지막 기회. 되면 이 메일이 요약을 싣고, 안 되면 요약 없이 나간다. **어느 쪽이든 메일은 여기서 멈추지 않는다.**
            if (_withinDays > 0 && !tried.Contains(runKey))
            {
                await writer.EnsureAsync(runKey, retitle: false, ct);
            }

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
