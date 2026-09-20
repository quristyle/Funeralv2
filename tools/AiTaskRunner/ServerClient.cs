using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiTaskRunner;

/// <summary>
/// 서버(ProjMngServer)와 말하는 창구.
/// </summary>
/// <remarks>
/// <para>
/// <b>보고에 실패해도 작업은 계속한다.</b> 서버가 내려가 있다고 CLI 를 멈추면
/// 도구가 원래 하려던 일보다 큰 문제를 만든다 — 배포 도구가 먼저 정한
/// 규칙이고(<c>release-run.sh</c>) 여기서도 같다.
/// </para>
/// <para>
/// 연달아 실패하면 <b>보고를 포기한다.</b> 포기하지 않으면 서버가 내려가 있는
/// 동안 보고마다 타임아웃을 기다려 <b>작업 자체가 느려진다.</b>
/// </para>
/// </remarks>
public sealed class ServerClient(HttpClient http, RunnerOptions options, ILogger<ServerClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>줄 일이 있나 물어본다. 없으면 빈 목록이다.</summary>
    public async Task<List<Claim>> ClaimAsync(int capacity, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/ai-runner/claim")
            {
                Content = JsonContent.Create(new
                {
                    runnerName = options.Name,
                    // **어댑터에 적힌 것이 곧 돌릴 수 있는 것이다**
                    // (RunnerOptions.RunnableKinds 주석). 여기 빠진 종류는
                    // 서버가 아예 안 건네주므로 그 건은 조용히 「대기」에 남는다.
                    kinds = options.RunnableKinds.ToArray(),
                    capacity,
                }, options: Json),
            };

            req.Headers.Add("X-AiTask-Token", options.RunnerToken);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("집어가기가 거절됐습니다: HTTP {Code}", (int)res.StatusCode);
                return [];
            }

            var body = await res.Content.ReadFromJsonAsync<Envelope<Claim>>(Json, ct);
            return body?.Data?.Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("서버에 물어보지 못했습니다: {Message}", ex.Message);
            return [];
        }
    }

    /// <summary>살아 있다고 알린다. 취소 요청이 왔으면 참을 돌려준다.</summary>
    /// <returns>
    /// <c>null</c> 이면 <b>서버가 이 실행을 모른다</b>(끝났거나 토큰이 다르다).
    /// 그때 실행기는 보고를 멈추되 작업은 계속한다.
    /// </returns>
    public async Task<bool?> HeartbeatAsync(long runKey, string token, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"/api/ai-runner/runs/{runKey}/heartbeat");

            req.Headers.Add("X-AiTask-Token", token);

            using var res = await http.SendAsync(req, ct);

            if (res.StatusCode is System.Net.HttpStatusCode.Forbidden
                or System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            var body = await res.Content.ReadFromJsonAsync<Envelope<Heartbeat>>(Json, ct);
            return body?.Data?.Result?.FirstOrDefault()?.CancelRequested ?? false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("하트비트 실패: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>로그를 밀어 올린다. 같은 구간을 두 번 보내도 서버가 한 번만 넣는다.</summary>
    public async Task<bool> LogsAsync(long runKey, string token, IReadOnlyList<LogLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            return true;
        }

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"/api/ai-runner/runs/{runKey}/logs")
            {
                Content = JsonContent.Create(new { lines }, options: Json),
            };

            req.Headers.Add("X-AiTask-Token", token);

            using var res = await http.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("로그 전송 실패: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>끝났다고 보고한다. <b>이것만은 여러 번 시도한다.</b></summary>
    /// <remarks>
    /// 이 보고가 안 가면 서버는 임대가 끊긴 것으로 보고 「중단」으로 남긴다 —
    /// 실제로는 성공했는데 화면이 실패로 말하는 쪽이라 특히 나쁘다.
    /// </remarks>
    public async Task CompleteAsync(long runKey, string token, object done, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(
                    HttpMethod.Post, $"/api/ai-runner/runs/{runKey}/complete")
                {
                    Content = JsonContent.Create(done, options: Json),
                };

                req.Headers.Add("X-AiTask-Token", token);

                using var res = await http.SendAsync(req, ct);

                if (res.IsSuccessStatusCode)
                {
                    return;
                }

                // 403/404 는 서버가 이미 끝난 것으로 본 것이다. 다시 보내도 같다.
                if (res.StatusCode is System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogWarning("완료 보고를 서버가 거절했습니다 (이미 끝난 실행): {RunKey}", runKey);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("완료 보고 {Attempt}/3 실패: {Message}", attempt, ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
        }

        logger.LogError("완료 보고를 끝내 보내지 못했습니다. 서버는 이 건을 중단으로 볼 것입니다: {RunKey}", runKey);
    }

    /// <summary>
    /// AI CLI 의 한도를 올린다.
    /// </summary>
    /// <remarks>
    /// <b>장비 토큰을 쓴다</b> — run 토큰이 아니다. 이 보고는 실행과 무관하게
    /// 주기적으로 올라오므로 묶일 run 이 없다(집어가기와 같은 토큰이다).
    ///
    /// <para>
    /// <b>실패해도 다시 보내지 않는다.</b> 15분 뒤에 어차피 같은 것을 올린다 —
    /// 여기서 재시도를 쌓으면 서버가 내려가 있는 동안 곁들이는 일이 본업의
    /// 시간을 먹는다.
    /// </para>
    /// </remarks>
    public async Task UsageAsync(AiUsageReport report, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/ai-runner/usage")
            {
                Content = JsonContent.Create(report, options: Json),
            };

            req.Headers.Add("X-AiTask-Token", options.RunnerToken);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("사용량 보고가 거절됐습니다: HTTP {Code}", (int)res.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("사용량 보고 실패: {Message}", ex.Message);
        }
    }

    // ── 오가는 모양 ─────────────────────────────────────────

    public sealed class Envelope<T>
    {
        public bool Success { get; set; }
        public Payload<T>? Data { get; set; }
    }

    public sealed class Payload<T>
    {
        public List<T>? Result { get; set; }
    }

    public sealed class Heartbeat
    {
        public bool CancelRequested { get; set; }
    }

    public sealed class Claim
    {
        public long RunKey { get; set; }
        public string Token { get; set; } = string.Empty;
        public long TaskKey { get; set; }
        public string? Title { get; set; }
        public string? Instruction { get; set; }
        public string? RunnerKind { get; set; }
        public int TimeoutMinutes { get; set; }
        public bool AutoPush { get; set; }
        public string? TargetRef { get; set; }
        public Target? Target { get; set; }
    }

    public sealed class Target
    {
        public long TargetKey { get; set; }
        public string? TargetNm { get; set; }
        public string? TargetKind { get; set; }
        public string? TargetPath { get; set; }
        public string? RepoUrl { get; set; }
        public string? DefaultRef { get; set; }
        public string? IsolationMode { get; set; }
        public int? MaxSizeMb { get; set; }
        public bool AllowPush { get; set; }
        public string? PushRef { get; set; }
        public string? GateMode { get; set; }
    }
}

/// <summary>로그 한 줄.</summary>
public sealed class LogLine
{
    public int Seq { get; set; }
    public string Stream { get; set; } = "stdout";
    public string Text { get; set; } = string.Empty;
}
