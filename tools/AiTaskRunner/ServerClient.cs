using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiTaskRunner;

/// <summary>
/// 서버(ProjMngServer)와 말하는 창구. 보고에 실패해도 작업은 계속한다 — 서버가 내려가 있다고 CLI 를 멈추면 원래 일보다 큰 문제가 된다.
/// 연달아 실패하면 보고를 포기한다(매번 타임아웃을 기다리면 작업이 느려진다). catch 들은 취소를 가른다 — 멈추라는 신호만 밖으로 내보내고, HttpClient 제한 시간은 실패로 적는다.
/// </summary>
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
                    // 어댑터에 적힌 것이 곧 돌릴 수 있는 것이다(RunnerOptions.RunnableKinds). 빠진 종류는 서버가 건네주지 않아 그 건은 대기에 남는다.
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
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("서버에 물어보지 못했습니다: {Message}", ex.Message);
            return [];
        }
    }

    /// <summary>지시에 붙어 온 파일 하나를 받아 path 에 적는다. run 토큰으로 연다(그 실행의 파일만 내준다).
    /// 못 받아도 작업은 계속한다 — 지시문에 못 받았다고 적어야 AI 가 없는 파일을 있는 것처럼 찾지 않는다.</summary>
    public async Task<bool> DownloadFileAsync(
        long runKey, string token, long fileKey, string path, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get, $"/api/ai-runner/runs/{runKey}/files/{fileKey}");

            req.Headers.Add("X-AiTask-Token", token);

            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "첨부 {FileKey} 를 받지 못했습니다: HTTP {Code}", fileKey, (int)res.StatusCode);
                return false;
            }

            await using var target = File.Create(path);
            await res.Content.CopyToAsync(target, ct);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("첨부 {FileKey} 를 받지 못했습니다: {Message}", fileKey, ex.Message);
            return false;
        }
    }

    /// <summary>이 장비가 들여다볼 대상 목록. 「대상 git 상태」 화면이 쓰는 값이다. 집어가기와 같은 장비 토큰을 쓴다(run 토큰은 실행에 묶여 이 일에는 못 쓴다).</summary>
    public async Task<List<ProbeTarget>> ProbeTargetsAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get, $"/api/ai-runner/targets?runnerName={Uri.EscapeDataString(options.Name)}");

            req.Headers.Add("X-AiTask-Token", options.RunnerToken);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogDebug("대상 목록을 받지 못했습니다: HTTP {Code}", (int)res.StatusCode);
                return [];
            }

            var body = await res.Content.ReadFromJsonAsync<Envelope<ProbeTarget>>(Json, ct);
            return body?.Data?.Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 15초마다 도는 감시라 로그 수위를 낮게 둔다 — 서버가 잠깐 내려가면 경고가 분당 넷씩 쌓인다.
            logger.LogDebug("대상 목록을 묻지 못했습니다: {Message}", ex.Message);
            return [];
        }
    }

    /// <summary>들여다본 결과를 올린다. 못 올려도 다음 바퀴에 다시 본다 — 스냅샷이라 밀린 것을 모아 둘 이유가 없다.</summary>
    public async Task ReportTargetStatusAsync(
        IReadOnlyList<GitProbe.Snapshot> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/ai-runner/target-status")
            {
                Content = JsonContent.Create(
                    new { runnerName = options.Name, items }, options: Json),
            };

            req.Headers.Add("X-AiTask-Token", options.RunnerToken);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogDebug("대상 상태 보고를 거절당했습니다: HTTP {Code}", (int)res.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("대상 상태를 보고하지 못했습니다: {Message}", ex.Message);
        }
    }

    // ── 처리 파일 대 보기 ───────────────────────────────────
    // 대상 상태와 같은 까닭 — 서버는 컨테이너 안이라 대상 경로를 볼 수 없어, 파일이 있는지는 호스트 쪽(FileProbe)만 답할 수 있다.

    /// <summary>대 볼 것이 있나 물어본다. 없으면 빈 목록이다.</summary>
    public async Task<List<FileCheckJob>> FileCheckJobsAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/ai-runner/file-checks?runnerName={Uri.EscapeDataString(options.Name)}");

            req.Headers.Add("X-AiTask-Token", options.RunnerToken);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                // 404 는 고장이 아니다 — 서버가 이 길을 아직 모르는 판일 수 있다(실행기와 서버는 따로 배포된다).
                logger.LogDebug("대 볼 목록을 받지 못했습니다: HTTP {Code}", (int)res.StatusCode);
                return [];
            }

            var body = await res.Content.ReadFromJsonAsync<Envelope<FileCheckJob>>(Json, ct);
            return body?.Data?.Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("대 볼 목록을 묻지 못했습니다: {Message}", ex.Message);
            return [];
        }
    }

    /// <summary>대 본 결과를 올린다. <b>못 올려도 다음 바퀴에 다시 묻는다.</b></summary>
    public async Task ReportFileChecksAsync(IReadOnlyList<FileCheckReport> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/ai-runner/file-checks")
            {
                Content = JsonContent.Create(
                    new { runnerName = options.Name, items }, options: Json),
            };

            req.Headers.Add("X-AiTask-Token", options.RunnerToken);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogDebug("대 본 결과를 거절당했습니다: HTTP {Code}", (int)res.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("대 본 결과를 올리지 못했습니다: {Message}", ex.Message);
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
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
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
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug("로그 전송 실패: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>끝났다고 보고한다. 이것만은 여러 번 시도한다 — 안 가면 서버가 임대가 끊긴 것으로 보고 「중단」으로 남긴다.</summary>
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
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning("완료 보고 {Attempt}/3 실패: {Message}", attempt, ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
        }

        logger.LogError("완료 보고를 끝내 보내지 못했습니다. 서버는 이 건을 중단으로 볼 것입니다: {RunKey}", runKey);
    }

    /// <summary>AI CLI 의 한도를 올린다. 장비 토큰을 쓰고(주기 보고라 묶일 run 이 없다), 실패해도 다시 보내지 않는다 — 15분 뒤에 같은 것을 올리므로 재시도가 본업의 시간을 먹지 않게 한다.</summary>
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
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // 묻어 두지 않는다 — 15분에 한 번이라 시끄러울 일이 없고, 계속 실패하면 화면의 한도 칸이 옛 숫자로 굳는다.
            logger.LogWarning("사용량 보고 실패: {Message}", ex.Message);
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

        /// <summary>지시에 함께 올라온 파일들. 이름과 크기뿐이다 — 바이트는 DownloadFileAsync 로 하나씩 받는다.</summary>
        public List<ClaimFile> Files { get; set; } = [];
    }

    /// <summary>지시에 붙어 온 파일 한 개.</summary>
    public sealed class ClaimFile
    {
        public long FileKey { get; set; }
        public string FileNm { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long ByteSize { get; set; }
        public bool IsImage { get; set; }
    }

    /// <summary>들여다볼 대상 한 줄. <b>경로 말고는 거의 안 온다.</b></summary>
    public sealed class ProbeTarget
    {
        public long TargetKey { get; set; }
        public string? TargetNm { get; set; }
        public string? TargetPath { get; set; }
        public string? TargetKind { get; set; }

        /// <summary>화면이 「지금 확인」을 눌렀다. 주기를 기다리지 않는다.</summary>
        public bool ProbeRequested { get; set; }

        /// <summary>마지막으로 본 시각. <b>서버가 찍은 값</b>이다.</summary>
        public DateTime? ProbedAt { get; set; }
    }

    /// <summary>대 볼 실행 한 건.</summary>
    public sealed class FileCheckJob
    {
        public long RunKey { get; set; }

        /// <summary>대상 위치 — <b>경로를 재는 뿌리</b>다.</summary>
        public string? TargetPath { get; set; }

        public string? TargetKind { get; set; }

        /// <summary>그때 일하던 자리. 아직 남아 있으면 거기도 본다.</summary>
        public string? WorkspacePath { get; set; }

        /// <summary>그때 쓰던 가지. 대상 작업본이 뒤처져 있어도 거기서는 보인다.</summary>
        public string? GitBranch { get; set; }

        public List<string> Paths { get; set; } = [];
    }

    /// <summary>한 실행에 대한 답 묶음.</summary>
    public sealed class FileCheckReport
    {
        public long RunKey { get; set; }

        public List<FileCheckAnswer> Results { get; set; } = [];
    }

    /// <summary>한 줄의 답. <c>state</c> 는 <c>found</c>·<c>missing</c>·<c>ambiguous</c>.</summary>
    public sealed class FileCheckAnswer
    {
        public string Raw { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string State { get; set; } = "missing";
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
