using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 멈춘 것을 찾아내는 감시자.
/// </summary>
/// <remarks>
/// <para>
/// 이 기능에서 <b>가장 나쁜 실패는 조용히 멈춰 있는 것</b>이다 —
/// 「요청」인 채로 아무도 집어 가지 않거나, 「실행중」인 채로 실행기와
/// 연락이 끊기거나. 둘 다 화면에서는 똑같이 <i>돌고 있는 것처럼</i> 보인다.
/// </para>
/// <para>
/// 그래서 주기적으로 셋을 본다 — 집어가기 제한 시간 · 임대 만료 · 고아 잠금.
/// </para>
/// </remarks>
public sealed class AiStaleSweeper(
    IConfiguration configuration, ILogger<AiStaleSweeper> logger) : BackgroundService
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 큐에 넣고 이 시간 안에 아무도 집어 가지 않으면 중단으로 본다.
    /// </summary>
    /// <remarks>
    /// 배포 도구가 먼저 쓰던 값이다(<c>Release:PickupTimeoutSeconds</c>) —
    /// <b>소비자가 안 떠 있다는 뜻이라 빨리 알려 주는 편이 낫다.</b>
    /// </remarks>
    private readonly int _pickupTimeout = configuration.GetValue("AiTasks:PickupTimeoutSeconds", 60);

    /// <summary>
    /// 로그 줄을 며칠 두나. 0 이면 안 지운다.
    /// </summary>
    /// <remarks>
    /// <b>실행 기록은 남기고 줄만 지운다.</b> 나중에 다시 보는 것은 대개
    /// 결과문이고, 수천 줄짜리 로그는 그때 쓸모가 거의 없다.
    /// </remarks>
    private readonly int _keepLogDays = configuration.GetValue("AiTasks:KeepLogDays", 90);

    private readonly TimeSpan _period =
        TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("AiTasks:SweepSeconds", 20)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            logger.LogWarning("연결 문자열이 없어 AI 작업 감시자를 돌리지 않습니다.");
            return;
        }

        logger.LogInformation("AI 작업 감시자 시작 (주기 {Sec}초, 집어가기 제한 {Pickup}초)",
            _period.TotalSeconds, _pickupTimeout);

        using var timer = new PeriodicTimer(_period);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // 한 번 실패했다고 감시자를 죽이지 않는다. 죽으면 그때부터
                // 아무도 안 보는데 그 사실조차 안 보인다.
                logger.LogError(ex, "AI 작업 감시 한 바퀴가 실패했습니다.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);

        // ① 임대 만료 — 실행기와 연락이 끊겼다.
        //
        //    **자동으로 다시 돌리지 않는다.** 이 작업은 파일을 고치므로,
        //    네트워크만 끊긴 경우 CLI 는 아직 돌고 있을 수 있다. 거기서 같은
        //    일을 또 주면 둘이 같은 저장소를 고친다(설계 6.11).
        var interrupted = await db.QueryAsync<long>("""
            UPDATE projmng.ai_task_run
               SET run_status  = 'interrupted',
                   finished_at = now(),
                   error_summary = COALESCE(error_summary,
                       '실행기와 연락이 끊겼습니다. 무엇까지 됐는지는 알 수 없습니다.'),
                   lease_expires_at = NULL
             WHERE finished_at IS NULL
               AND lease_expires_at IS NOT NULL
               AND lease_expires_at < now()
            RETURNING run_key
            """);

        var lost = interrupted.ToList();

        if (lost.Count > 0)
        {
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET task_status  = 'interrupted',
                       request_flag = 'none',
                       finished_at  = now(),
                       last_error   = '실행기와 연락이 끊겼습니다. 다시 요청하려면 사람이 눌러야 합니다.',
                       title_run_key = COALESCE(title_run_key, last_run_key),
                       row_version  = row_version + 1
                 WHERE last_run_key = ANY(@lost)
                """, new { lost = lost.ToArray() });

            // 대상 잠금을 푼다. 안 풀면 그 대상이 영영 막힌다.
            await db.ExecuteAsync(
                "UPDATE projmng.ai_target SET running_run_key = NULL WHERE running_run_key = ANY(@lost)",
                new { lost = lost.ToArray() });

            logger.LogWarning("임대가 끊긴 실행 {Count}건을 중단으로 표시했습니다: {Keys}",
                lost.Count, string.Join(",", lost));
        }

        // ② 집어가기 제한 시간 — 큐에 넣었는데 아무도 안 집었다.
        //
        //    **실행기가 안 떠 있다는 뜻**이고, 그것이 이 기능의 가장 흔한 고장이다.
        //    상태를 바꾸지 않고 사유만 적는다 — 실행기가 늦게 떠서 집어 갈 수도
        //    있으므로, 여기서 '실패' 로 못 박으면 그 뒤 보고가 갈 곳을 잃는다.
        //
        //    **고른 AI 이름을 사유에 박아 넣는다.** 실행기가 멀쩡히 떠 있는데도
        //    이 자리에 오는 길이 하나 더 있기 때문이다 — 집어가기 질의는
        //    `runner_kind = ANY(실행기가 말한 종류)` 로 거르므로, **그 장비가
        //    그 CLI 를 안 가졌으면 아무 일도 안 일어난다.** 오류도 로그도 없다.
        //    실제로 copilot 을 넣고 장비 설정을 옛것으로 둔 동안 코파일럿으로
        //    시킨 건이 전부 여기 앉아 있었고, 그때 화면이 한 말이
        //    「실행기가 떠 있는지 확인하십시오」였다 — **떠 있었다.** 그래서
        //    엉뚱한 곳을 한참 봤다. 이름 한 낱말이 그 시간을 없앤다.
        var stalled = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET last_error = '「' || runner_kind || '」 를 돌릴 실행기가 집어 가지 않았습니다.'
                                || ' 실행기가 떠 있는지, 그 실행기가 「' || runner_kind
                                || '」 를 가졌는지 확인하십시오.'
             WHERE task_status = 'queued'
               AND requested_at < now() - make_interval(secs => @pickup)
               -- **옛 사유가 적힌 건은 한 번 덮어쓴다.** 걸러 내는 무늬를
               -- 새 사유에만 맞춰 두면, 이미 「실행기가 떠 있는지 확인하십시오」
               -- 로만 앉아 있던 건도 다음 바퀴에 AI 이름이 박힌 사유로 바뀐다.
               -- 그 뒤로는 이 무늬에 걸려 더 안 건드린다.
               AND (last_error IS NULL
                    OR last_error NOT LIKE '%를 돌릴 실행기가 집어 가지 않았습니다%')
            """, new { pickup = _pickupTimeout });

        if (stalled > 0)
        {
            logger.LogWarning("{Count}건이 {Sec}초 안에 집혀 가지 않았습니다. 실행기가 떠 있습니까?",
                stalled, _pickupTimeout);
        }

        // ③ 고아 잠금 — 실행은 끝났는데 대상이 잠긴 채 남았다.
        //
        //    ①·② 로 대부분 풀리지만, 완료 보고 중간에 서버가 내려가면 남을 수
        //    있다. 그때 증상이 **「그 대상만 아무것도 안 돈다」** 라서 원인을
        //    찾기가 특히 어렵다.
        var orphan = await db.ExecuteAsync("""
            UPDATE projmng.ai_target t
               SET running_run_key = NULL
             WHERE t.running_run_key IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM projmng.ai_task_run r
                    WHERE r.run_key = t.running_run_key AND r.finished_at IS NULL )
            """);

        if (orphan > 0)
        {
            logger.LogWarning("끝난 실행이 잡고 있던 대상 잠금 {Count}건을 풀었습니다.", orphan);
        }

        // (4) 오래된 로그 줄.
        //
        //     **정하지 않으면 반드시 이 표가 제일 커진다.** 실행 한 번에 수천
        //     줄이 쌓이고 지우는 사람이 없다. 실행 기록(결과문·바뀐 파일)은
        //     남기고 **줄만** 지운다 — 나중에 다시 보는 것은 대개 결과문이다.
        if (_keepLogDays > 0)
        {
            var purged = await db.ExecuteAsync(
                "DELETE FROM projmng.ai_task_log WHERE log_at < now() - make_interval(days => @days)",
                new { days = _keepLogDays });

            if (purged > 0)
            {
                logger.LogInformation("{Days}일 지난 로그 {Count}줄을 지웠습니다.", _keepLogDays, purged);
            }
        }
    }
}
