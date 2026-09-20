using System.Diagnostics;
using System.Text;

namespace AiTaskRunner;

/// <summary>
/// AI CLI 의 <c>/usage</c> 를 주기적으로 읽어 서버로 올린다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 실행기가 하나.</b> 한도를 아는 것은 CLI 이고, 그 CLI 와 그 계정의
/// 로그인 상태는 이 장비에만 있다. 웹 서버가 읽으려면 웹 요청을 받는
/// 프로세스에 셸을 여는 권한을 줘야 하는데, 이 기능 전체가 그러지 않으려고
/// 갈라져 있다(설계 9.8).
/// </para>
/// <para>
/// <b>작업과 섞지 않는다.</b> 별도의 배경 작업으로 돈다 — 사용량 조회가
/// 느리거나 매달려도 집어가기와 하트비트는 그대로 돌아야 한다.
/// </para>
/// <para>
/// <b>한도를 못 읽어도 보고는 간다.</b> 「장비가 살아 있다」는 사실이 그
/// 요청에 함께 실려 있고, 화면은 그것으로 실행기가 붙어 있는지를 말한다.
/// 실패는 실패대로 적어 올린다 — 「오래된 값」과 「읽지 못하는 중」은
/// 사람이 할 일이 다르다.
/// </para>
/// <para>
/// <b>명령을 코드에 박지 않는다.</b> 어댑터마다 <c>UsageArgs</c> 로 적는다.
/// CLI 의 플래그는 자주 바뀌고, 박아 두면 플래그 한 글자 때문에 배포한다 —
/// 실행 인자를 설정으로 뺀 것과 같은 이유다. <b>비워 두면 그 CLI 는
/// 건너뛴다</b>(사용량을 말해 주지 않는 CLI 가 있다).
/// </para>
/// </remarks>
public sealed class UsageReporter(
    RunnerOptions options, ServerClient server, ILogger<UsageReporter> logger) : BackgroundService
{
    /// <summary>출력에서 들고 갈 길이 상한. 서버 쪽 칸도 4000 이다.</summary>
    private const int MaxRawLength = 4000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.UsageEnabled)
        {
            logger.LogInformation("사용량 보고가 꺼져 있습니다 (Runner:UsageEnabled).");
            return;
        }

        // 기동 직후에 한 번 올린다. **첫 값이 15분 뒤에 오면** 실행기를 막
        // 올린 사람이 화면에서 아무것도 못 보고 「안 되는구나」로 읽는다.
        // 다만 집어가기가 먼저 자리를 잡게 몇 초 비켜 준다.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        var period = TimeSpan.FromMinutes(Math.Clamp(options.UsageIntervalMinutes, 1, 24 * 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 곁들이는 일이다. 여기서 터져 실행기가 죽으면 본업이 멈춘다.
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
            if (string.IsNullOrWhiteSpace(adapter.Executable) || adapter.UsageArgs.Length == 0)
            {
                continue;
            }

            report.Items.Add(await ReadAsync(kind, adapter, ct));
        }

        await server.UsageAsync(report, ct);
    }

    /// <summary>
    /// CLI 하나에게 물어본다.
    /// </summary>
    /// <remarks>
    /// <b>셸을 거치지 않는다.</b> <c>UseShellExecute = false</c> 에 인자는
    /// <c>ArgumentList</c> 로 하나씩 넣는다 — <see cref="CliRunner"/> 와 같은 규칙이다.
    /// 여기 들어가는 글자는 설정 파일에서 오지만, 통로를 둘로 만들지 않는다.
    /// </remarks>
    private async Task<AiUsageItem> ReadAsync(
        string kind, AdapterOptions adapter, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(adapter.Executable)
        {
            // **작업공간이 아니라 장비의 집이다.** 한도는 계정에 딸린 값이라
            // 어느 폴더에서 묻든 같고, 지워질 수 있는 작업공간을 가리키면
            // 그 폴더가 사라진 뒤로 조용히 실패한다.
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

            // 물어보기만 하는 호출이라 줄 것이 없다. **닫아 주지 않으면**
            // 입력을 기다리는 CLI 가 제한 시간까지 매달린다.
            proc.StandardInput.Close();

            using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timer.CancelAfter(timeout);

            var stdout = proc.StandardOutput.ReadToEndAsync(timer.Token);
            var stderr = proc.StandardError.ReadToEndAsync(timer.Token);

            await proc.WaitForExitAsync(timer.Token);

            var raw = Cut($"{await stdout}\n{await stderr}".Trim());

            if (proc.ExitCode != 0 && raw.Length == 0)
            {
                return Failed(kind, $"종료 코드 {proc.ExitCode}");
            }

            var item = UsageText.Parse(kind, raw);

            // **아무 숫자도 못 읽었으면 성공이 아니다.** 명령은 돌았는데
            // 출력 형식이 바뀐 경우가 여기다 — 화면이 빈 칸을 「0% 썼다」로
            // 읽지 않게 실패로 남기고, 원문은 그대로 올린다.
            if (item is { SessionPct: null, WeekPct: null, WeekOpusPct: null,
                          RemainingTokens: null, LimitTokens: null })
            {
                item.Ok = false;
                item.ErrorText = "출력에서 한도를 찾지 못했습니다. 원문을 확인하십시오.";
            }

            return item;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failed(kind, $"{timeout.TotalSeconds:0}초 안에 답하지 않았습니다.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(kind, ex.Message);
        }
    }

    private static AiUsageItem Failed(string kind, string why)
        => new() { RunnerKind = kind, Ok = false, ErrorText = why };

    private static string Cut(string text)
        => text.Length <= MaxRawLength ? text : text[..MaxRawLength];
}
