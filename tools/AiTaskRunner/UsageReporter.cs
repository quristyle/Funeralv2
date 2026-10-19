using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// AI CLI 의 한도를 주기적으로 읽어 서버로 올린다. 한도를 아는 것은 이 장비의 CLI 와 로그인이라 실행기가 맡고, 집어가기·하트비트와 섞지 않는 별도 배경 작업으로 돈다.
/// 한도를 못 읽어도 보고는 간다 — 「장비가 살아 있다」와 실패를 함께 올려 「오래된 값」과 「읽지 못하는 중」을 화면이 가르게 한다.
/// 묻는 길(명령 UsageArgs 또는 주소 UsageUrl)은 어댑터 설정에 적는다. 둘 다 비어 있으면 그 CLI 는 건너뛴다.
/// </summary>
public sealed class UsageReporter(
    RunnerOptions options,
    ServerClient server,
    IHttpClientFactory httpFactory,
    ILogger<UsageReporter> logger) : BackgroundService
{
    /// <summary>받은 것을 들고 갈 길이 상한. 화면에 실리는 길이가 아니다(서버가 4000 으로 자른다). 여기서 같이 자르면 claude 의 JSON 봉투가 열리지 않게 된다.</summary>
    private const int MaxRawLength = 16000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.UsageEnabled)
        {
            logger.LogInformation("사용량 보고가 꺼져 있습니다 (Runner:UsageEnabled).");
            return;
        }

        // 기동 직후에 한 번 올린다 — 첫 값이 15분 뒤에 오면 올린 사람이 「안 되는구나」로 읽는다. 집어가기가 먼저 자리를 잡게 몇 초 비켜 준다.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        var period = TimeSpan.FromMinutes(Math.Clamp(options.UsageIntervalMinutes, 1, 24 * 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException
                                       || !stoppingToken.IsCancellationRequested)
            {
                // 곁들이는 일이라 여기서 실행기를 죽이면 본업이 멈춘다.
                // 멈추라는 신호로 온 취소만 내보낸다 — HttpClient 제한 시간(30초)에 걸린 것은 실패로 적는다. 아니면 StopHost 로 돌던 작업까지 죽는다.
                logger.LogWarning("사용량 보고가 실패했습니다: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(period, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReportAsync(CancellationToken ct)
    {
        var report = new AiUsageReport
        {
            RunnerName = options.Name,
            HostName = Environment.MachineName,
            RunnerVersion = typeof(UsageReporter).Assembly.GetName().Version?.ToString(),
            Kinds = [.. options.RunnableKinds],
        };

        foreach (var (kind, adapter) in options.Adapters)
        {
            // 이 장비에서 끈 CLI 는 묻지 않는다 — 끄는 법은 Executable 을 비우는 것 하나다.
            if (string.IsNullOrWhiteSpace(adapter.Executable))
            {
                continue;
            }

            var hasUrl = !string.IsNullOrWhiteSpace(adapter.UsageUrl);

            if (!hasUrl && adapter.UsageArgs.Length == 0)
            {
                continue;
            }

            report.Items.AddRange(hasUrl
                ? await ReadHttpAsync(kind, adapter, ct)
                : await ReadCliAsync(kind, adapter, ct));
        }

        await server.UsageAsync(report, ct);
    }

    // ── CLI 에게 묻는 길 ────────────────────────────────────

    /// <summary>CLI 하나를 띄워 물어본다. 셸을 거치지 않고 인자는 ArgumentList 로 넣는다 — CliRunner 와 같은 규칙이다.</summary>
    private async Task<List<AiUsageItem>> ReadCliAsync(
        string kind, AdapterOptions adapter, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(adapter.Executable)
        {
            // 작업공간이 아니라 장비의 집에서 묻는다 — 한도는 계정에 딸린 값이라 어디서 묻든 같고, 작업공간은 지워질 수 있다.
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in adapter.UsageArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(adapter.UsageTimeoutSeconds, 5, 600));

        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("프로세스를 띄우지 못했습니다.");

            // 물어보기만 하는 호출이라 줄 것이 없다. 닫아 주지 않으면 CLI 가 입력을 기다리며 제한 시간까지 매달린다.
            proc.StandardInput.Close();

            using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timer.CancelAfter(timeout);

            var stdout = proc.StandardOutput.ReadToEndAsync(timer.Token);
            var stderr = proc.StandardError.ReadToEndAsync(timer.Token);

            await proc.WaitForExitAsync(timer.Token);

            var raw = Cut($"{await stdout}\n{await stderr}".Trim());

            if (proc.ExitCode != 0 && raw.Length == 0)
            {
                return [Failed(kind, $"종료 코드 {proc.ExitCode}")];
            }

            return Interpret(kind, adapter, raw);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return [Failed(kind, $"{timeout.TotalSeconds:0}초 안에 답하지 않았습니다.")];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [Failed(kind, ex.Message)];
        }
    }

    // ── 주소로 묻는 길 ──────────────────────────────────────

    /// <summary>한도 주소를 한 번 두드린다. CLI 로 못 묻는 것만의 길이다(지금은 copilot). 토큰은 CLI 가 둔 자리에서 매번 읽고, 없으면 실패로 남긴다.</summary>
    private async Task<List<AiUsageItem>> ReadHttpAsync(
        string kind, AdapterOptions adapter, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Clamp(adapter.UsageTimeoutSeconds, 5, 600));

        try
        {
            var token = UsageToken.Read(adapter.UsageTokenFile, adapter.UsageTokenPath);

            if (!string.IsNullOrWhiteSpace(adapter.UsageTokenFile) && token is null)
            {
                return [Failed(kind,
                    $"{adapter.UsageTokenFile} 의 {adapter.UsageTokenPath} 에서 토큰을 찾지 못했습니다. "
                    + "그 CLI 로 로그인돼 있는지 확인하십시오.")];
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, adapter.UsageUrl);

            if (token is not null)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("token", token);
            }

            using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timer.CancelAfter(timeout);

            var http = httpFactory.CreateClient("usage");

            using var res = await http.SendAsync(req, timer.Token);

            var raw = Cut((await res.Content.ReadAsStringAsync(timer.Token)).Trim());

            if (!res.IsSuccessStatusCode)
            {
                // 본문을 원문으로 함께 올린다 — 401 인지 통로가 사라진 404 인지가 사람이 할 일을 가른다.
                return [Failed(kind, $"HTTP {(int)res.StatusCode}", raw)];
            }

            return Interpret(kind, adapter, raw);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return [Failed(kind, $"{timeout.TotalSeconds:0}초 안에 답하지 않았습니다.")];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [Failed(kind, ex.Message)];
        }
    }

    // ── 받은 것을 읽는다 ────────────────────────────────────

    /// <summary>받은 덩어리를 한도 칸들로 옮긴다. 읽어 낸 것이 없으면 ok = false 로 실패를 남긴다 — 명령은 돌았는데 출력 형식이 바뀐 경우라, 빈 칸을 「0% 썼다」로 읽지 않게 한다.</summary>
    private List<AiUsageItem> Interpret(string kind, AdapterOptions adapter, string raw)
    {
        List<AiUsageItem> items;

        try
        {
            items = adapter.UsageFormat?.ToLowerInvariant() switch
            {
                "agy" or "antigravity" => UsageAgy.Parse(kind, raw),
                "claude" => UsageClaude.Parse(kind, raw, adapter.UsageTranscriptRoot, adapter.UsageAuthFile),
                "copilot" => UsageCopilot.Parse(kind, raw),
                _ => [UsageText.Parse(kind, raw)],
            };
        }
        catch (Exception ex)
        {
            return [Failed(kind, $"출력을 읽지 못했습니다: {ex.Message}", raw)];
        }

        if (items.Count == 0)
        {
            return [Failed(kind, "출력에서 한도를 찾지 못했습니다. 원문을 확인하십시오.", raw)];
        }

        foreach (var item in items)
        {
            // 「무제한」처럼 까닭을 이미 적어 둔 칸은 건드리지 않는다 —
            // 숫자가 없는 것이 맞는 상태다.
            if (item is { SessionPct: null, WeekPct: null, WeekOpusPct: null, MonthPct: null,
                          RemainingTokens: null, LimitTokens: null, ErrorText: null })
            {
                item.Ok = false;
                item.ErrorText = "출력에서 한도를 찾지 못했습니다. 원문을 확인하십시오.";
            }
        }

        return items;
    }

    private static AiUsageItem Failed(string kind, string why, string? raw = null)
        => new() { RunnerKind = kind, Ok = false, ErrorText = why, RawText = raw };

    private static string Cut(string text)
        => text.Length <= MaxRawLength ? text : text[..MaxRawLength];
}
