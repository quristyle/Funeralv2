using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>멈춘 것을 찾아내는 감시자. 이 기능에서 가장 나쁜 실패는 조용히 멈춰 있는 것이다 — 「요청」인 채 아무도 집어 가지 않거나 「실행중」인 채 실행기와 연락이 끊기면, 화면에는 똑같이 돌고 있는 것처럼 보인다.</summary>
public sealed class AiStaleSweeper(
    IConfiguration configuration, ILogger<AiStaleSweeper> logger) : BackgroundService
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>큐에 넣고 이 시간 안에 아무도 집어 가지 않으면 중단으로 본다. 소비자가 안 떠 있다는 뜻이라 빨리 알려 주는 편이 낫다.</summary>
    private readonly int _pickupTimeout = configuration.GetValue("AiTasks:PickupTimeoutSeconds", 60);

    /// <summary>로그 줄을 며칠 두나. 0 이면 안 지운다. 실행 기록은 남기고 줄만 지운다.</summary>
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
                // 한 번 실패했다고 감시자를 죽이지 않는다 — 죽으면 아무도 안 보는데 그 사실조차 안 보인다.
                logger.LogError(ex, "AI 작업 감시 한 바퀴가 실패했습니다.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);

        // ① 임대 만료 — 실행기와 연락이 끊겼다. 자동으로 다시 돌리지 않는다: 파일을 고치는 작업이라 CLI 가 아직 돌고 있을 수 있고, 같은 일을 또 주면 둘이 같은 저장소를 고친다.
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

        // ② 집어가기 제한 시간 — 실행기가 안 떠 있다는 뜻이라 이 기능의 가장 흔한 고장이다. 상태는 바꾸지 않고 사유만 적는다(늦게 떠서 집어 갈 수 있다). 사유에 고른 AI 이름을 넣는다.
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

        // ③ 고아 잠금 — 실행은 끝났는데 대상이 잠긴 채 남았다. 완료 보고 중에 서버가 내려가면 생기며, 증상이 「그 대상만 아무것도 안 돈다」라 찾기 어렵다.
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

        // ④ 오래된 로그 줄 — 정하지 않으면 이 표가 가장 커진다. 실행 기록(결과문·바뀐 파일)은 남기고 줄만 지운다.
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
