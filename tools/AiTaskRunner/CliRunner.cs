using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AiTaskRunner;

/// <summary>
/// CLI 하나를 띄우고, 출력을 흘려 보내고, 끝날 때까지 지킨다.
/// </summary>
/// <remarks>
/// <para>
/// <b>셸을 거치지 않는다.</b> <c>UseShellExecute = false</c> 에 인자는
/// <c>ArgumentList</c> 로 하나씩 넣는다 — 사람이 쓴 글에
/// <c>; rm -rf ~</c> 가 들어 있어도 인자 한 개로 전달되게 하는 것이
/// 유일하게 믿을 수 있는 방어다.
/// </para>
/// </remarks>
public sealed partial class CliRunner(ILogger<CliRunner> logger)
{
    /// <summary>ANSI 색 코드. 안 지우면 화면이 <c>[32m</c> 범벅이 된다.</summary>
    [GeneratedRegex(@"\x1B\[[0-9;?]*[A-Za-z]")]
    private static partial Regex Ansi();

    public async Task<CliResult> RunAsync(
        AdapterOptions adapter,
        string workspace,
        string instruction,
        string promptPath,
        TimeSpan timeout,
        Func<LogLine, Task> onLine,
        Func<Task<bool>> isCanceled,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(adapter.Executable)
        {
            WorkingDirectory = workspace,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in adapter.Args)
        {
            psi.ArgumentList.Add(arg);
        }

        // 작업 폴더를 인자로도 알려 준다. cwd 만으로는 못 찾는 CLI 가 있다
        // (AdapterOptions.WorkspaceArgs 주석 — 실제로 밟음).
        foreach (var arg in adapter.WorkspaceArgs)
        {
            psi.ArgumentList.Add(arg.Replace("{path}", workspace));
        }

        var viaStdin = string.Equals(adapter.PromptVia, "stdin", StringComparison.OrdinalIgnoreCase);

        if (!viaStdin)
        {
            // 인자로 준다. **길면 파일 참조로 바꾼다** — 리눅스는 인자 하나가
            // 128KB 를 못 넘고(MAX_ARG_STRLEN), 긴 지시문이면 거기 먼저 걸린다.
            var bytes = Encoding.UTF8.GetByteCount(instruction);

            var text = bytes <= adapter.PromptMaxBytes
                ? instruction
                : adapter.PromptFileFallback.Replace("{path}", promptPath);

            if (bytes > adapter.PromptMaxBytes)
            {
                logger.LogInformation(
                    "지시문이 {Bytes}바이트라 파일 참조로 바꿉니다 ({Path}).", bytes, promptPath);
            }

            psi.ArgumentList.Add(adapter.PromptArgPrefix + text);
        }

        // git 이 자격을 물어보려고 멈추는 것을 막는다. 없으면 **타임아웃까지
        // 매달린 채로** 실패한다.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var seq = 0;
        var tail = new Queue<string>();
        var jsonResult = (string?)null;
        var sessionId = (string?)null;

        async Task EmitAsync(string raw, string stream)
        {
            var text = Ansi().Replace(raw, string.Empty);

            // stream-json 이면 줄마다 JSON 이다. 사람이 읽을 로그로도 남기되
            // **결과문과 세션 id 는 가려서 따로 들고 간다**(설계 4.2).
            if (adapter.ResultFrom == "stream-json" && text.StartsWith('{'))
            {
                var picked = StreamJson.Pick(text);

                if (picked.Result is { Length: > 0 })
                {
                    jsonResult = picked.Result;
                }

                if (picked.SessionId is { Length: > 0 })
                {
                    sessionId = picked.SessionId;
                }

                // **빈 글자도 그대로 받는다.** 「보여 줄 것이 없다」는 뜻이라
                // 아래에서 그 줄을 버린다. 여기서 빈 값을 걸러 내면 원문 JSON 이
                // 그대로 로그에 남아 오히려 더 읽기 어려워진다(실제로 밟음).
                text = picked.Human ?? text;
            }

            if (text.Length == 0)
            {
                return;
            }

            // 마지막 줄들을 들고 있는다. stream-json 이 아니면 이것이 결과문이 된다.
            tail.Enqueue(text);

            while (tail.Count > 40)
            {
                tail.Dequeue();
            }

            await onLine(new LogLine { Seq = ++seq, Stream = stream, Text = text });
        }

        proc.Start();

        // 지시문을 stdin 으로 준다. **반드시 닫는다** — 안 닫으면 CLI 가
        // 입력을 더 기다리며 영영 멈춘다(가장 흔한 「왜 안 끝나지」다).
        if (viaStdin)
        {
            await proc.StandardInput.WriteAsync(instruction.AsMemory(), ct);
        }

        proc.StandardInput.Close();

        var readOut = PumpAsync(proc.StandardOutput, "stdout", EmitAsync, ct);
        var readErr = PumpAsync(proc.StandardError, "stderr", EmitAsync, ct);

        var deadline = DateTime.UtcNow + timeout;
        var canceled = false;
        var timedOut = false;

        while (!proc.HasExited)
        {
            await Task.Delay(1000, ct);

            if (DateTime.UtcNow > deadline)
            {
                timedOut = true;
                break;
            }

            if (await isCanceled())
            {
                canceled = true;
                break;
            }
        }

        if (timedOut || canceled)
        {
            var why = timedOut ? "제한 시간을 넘겨" : "사람이 취소해";
            await EmitAsync($"[중단] {why} 프로세스를 멈춥니다.", "system");
            Kill(proc);
        }

        await proc.WaitForExitAsync(ct);
        await Task.WhenAll(readOut, readErr);

        var result = jsonResult ?? string.Join('\n', tail);

        return new CliResult
        {
            ExitCode = proc.ExitCode,
            TimedOut = timedOut,
            Canceled = canceled,
            ResultText = result,
            SessionId = sessionId,
            LineCount = seq,
        };
    }

    private static async Task PumpAsync(
        StreamReader reader, string stream, Func<string, string, Task> emit, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            await emit(line, stream);
        }
    }

    /// <summary>
    /// 프로세스를 <b>자식까지</b> 죽인다.
    /// </summary>
    /// <remarks>
    /// AI CLI 는 자식(node · git · ripgrep)을 띄우므로 부모만 죽이면 자식이
    /// 남아 장비에 쌓인다. 그것들이 계속 파일을 고치고 있을 수도 있다.
    /// </remarks>
    private void Kill(Process proc)
    {
        try
        {
            proc.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning("프로세스를 멈추지 못했습니다: {Message}", ex.Message);
        }
    }
}

public sealed class CliResult
{
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public bool Canceled { get; set; }

    /// <summary>AI 의 마지막 답. 화면과 메일이 둘 다 이것을 읽는다.</summary>
    public string? ResultText { get; set; }

    /// <summary>CLI 의 대화 id. 이어가기에 쓴다(설계 8-3.3).</summary>
    public string? SessionId { get; set; }

    public int LineCount { get; set; }
}
