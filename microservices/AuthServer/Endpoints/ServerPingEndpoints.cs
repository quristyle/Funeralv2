using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 내부 장비(192.168.219.229) Ping 모니터링 엔드포인트.
/// </summary>
public static class ServerPingEndpoints
{
    public const string DefaultTargetIp = "192.168.219.229";
    private const int DefaultTimeoutMs = 2000;

    public static void MapServerPingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/server-ping").WithTags("ServerPing");

        group.MapGet("", async (
            [FromQuery] string? ip,
            [FromQuery] int? timeout,
            CancellationToken ct) =>
        {
            var targetIp = string.IsNullOrWhiteSpace(ip) ? DefaultTargetIp : ip.Trim();
            var timeoutMs = timeout is > 0 and <= 10000 ? timeout.Value : DefaultTimeoutMs;

            var result = await PingServerAsync(targetIp, timeoutMs, ct);
            return Results.Ok(ApiResponse<ServerPingResultDto>.Ok(result));
        })
        .WithName("CheckServerPing");
    }

    internal static async Task<ServerPingResultDto> PingServerAsync(
        string targetIp, int timeoutMs, CancellationToken ct)
    {
        var dto = new ServerPingResultDto
        {
            TargetIp = targetIp,
            CheckedAt = DateTime.UtcNow,
        };

        var sw = Stopwatch.StartNew();

        // 1. .NET Ping 클래스 시도
        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync(targetIp, timeoutMs);

            sw.Stop();
            dto.RoundtripTimeMs = reply.RoundtripTime;
            dto.Status = reply.Status.ToString();

            if (reply.Status == IPStatus.Success)
            {
                dto.IsAlive = true;
                dto.Message = $"정상 응답 수신 ({dto.RoundtripTimeMs}ms)";
                return dto;
            }

            dto.IsAlive = false;
            dto.Message = $"장비 응답 없음 ({reply.Status})";
            return dto;
        }
        catch (PingException ex)
        {
            // Linux 권한 문제(Operation not permitted) 등 PingException 발생 시 프로세스 또는 소켓 폴백 시도
            return await FallbackPingAsync(targetIp, timeoutMs, ex.InnerException?.Message ?? ex.Message, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.IsAlive = false;
            dto.Status = "Error";
            dto.Message = ex.Message;
            return dto;
        }
    }

    private static async Task<ServerPingResultDto> FallbackPingAsync(
        string targetIp, int timeoutMs, string originalError, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // CLI ping 프로세스 실행 시도
        try
        {
            var timeoutSec = Math.Max(1, timeoutMs / 1000);
            var psi = new ProcessStartInfo
            {
                FileName = "ping",
                Arguments = $"-c 1 -W {timeoutSec} {targetIp}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
                var stderrTask = proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                sw.Stop();

                if (proc.ExitCode == 0)
                {
                    return new ServerPingResultDto
                    {
                        TargetIp = targetIp,
                        IsAlive = true,
                        RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                        Status = "Success",
                        Message = "정상 응답 수신 (CLI Ping)",
                        CheckedAt = DateTime.UtcNow,
                    };
                }

                // 권한 오류가 아닌 일반 타임아웃/Unreachable
                if (!stderr.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase))
                {
                    return new ServerPingResultDto
                    {
                        TargetIp = targetIp,
                        IsAlive = false,
                        RoundtripTimeMs = sw.ElapsedMilliseconds,
                        Status = "TimedOut",
                        Message = "장비 응답 없음 (CLI Ping 미응답)",
                        CheckedAt = DateTime.UtcNow,
                    };
                }
            }
        }
        catch
        {
            // 프로세스 실행 불가 시 다음 TCP 소켓 검사로 이동
        }

        // TCP 포트 검사 fallback (장비의 웹/관리 포트 또는 일반 포트 연결 여부 확인)
        int[] probePorts = [80, 443, 22, 8080];
        foreach (var port in probePorts)
        {
            try
            {
                using var tcp = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);

                var connectTask = tcp.ConnectAsync(targetIp, port, cts.Token).AsTask();
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs, ct)) == connectTask)
                {
                    sw.Stop();
                    return new ServerPingResultDto
                    {
                        TargetIp = targetIp,
                        IsAlive = true,
                        RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                        Status = "Success",
                        Message = $"정상 응답 수신 (TCP {port} 포트 연결됨)",
                        CheckedAt = DateTime.UtcNow,
                    };
                }
            }
            catch
            {
                // 다음 포트 시도
            }
        }

        sw.Stop();
        return new ServerPingResultDto
        {
            TargetIp = targetIp,
            IsAlive = false,
            RoundtripTimeMs = sw.ElapsedMilliseconds,
            Status = "Unreachable",
            Message = $"통신 불가 ({originalError})",
            CheckedAt = DateTime.UtcNow,
        };
    }
}
