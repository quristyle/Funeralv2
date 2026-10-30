using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using AuthServer.Data;
using AuthServer.Entities;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Endpoints;

/// <summary>
/// 내부 장비(그리드 중계 192.168.219.229 · jin sm 192.168.219.224 및 등록된 장비) Ping 모니터링 엔드포인트.
/// </summary>
public static class ServerPingEndpoints
{
    public const string DefaultTargetIp = "192.168.219.229";
    public const string JinSmTargetIp = "192.168.219.224";
    private const int DefaultTimeoutMs = 2000;

    public static string GetDefaultDeviceName(string ip) => ip switch
    {
        DefaultTargetIp => "그리드 중계 장비",
        JinSmTargetIp => "jin sm 장비",
        _ => "내부 장비",
    };

    public static void MapServerPingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/server-ping").WithTags("ServerPing");

        // 단일 IP Ping 검사 (기존 호환)
        group.MapGet("", async (
            [FromQuery] string? ip,
            [FromQuery] int? timeout,
            [FromQuery] string? name,
            CancellationToken ct) =>
        {
            var targetIp = string.IsNullOrWhiteSpace(ip) ? DefaultTargetIp : ip.Trim();
            var timeoutMs = timeout is > 0 and <= 10000 ? timeout.Value : DefaultTimeoutMs;

            var result = await PingServerAsync(targetIp, timeoutMs, ct, name);
            return Results.Ok(ApiResponse<ServerPingResultDto>.Ok(result));
        })
        .WithName("CheckServerPing");

        // 감시 대상 장비 목록 조회
        group.MapGet("/targets", async (
            [FromQuery] bool? activeOnly,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var query = db.ServerPingTargets
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (activeOnly == true)
            {
                query = query.Where(x => x.IsActive);
            }

            var targets = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => ToDto(x))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse<List<ServerPingTargetDto>>.Ok(targets));
        })
        .WithName("GetServerPingTargets");

        // 감시 대상 장비 추가
        group.MapPost("/targets", async (
            [FromBody] CreateServerPingTargetDto input,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(ApiResponse<ServerPingTargetDto>.Fail("장비명을 입력해 주세요."));

            if (string.IsNullOrWhiteSpace(input.Ip))
                return Results.BadRequest(ApiResponse<ServerPingTargetDto>.Fail("IP 주소 또는 도메인을 입력해 주세요."));

            var (normalizedHost, _) = NormalizeTarget(input.Ip);
            var effectiveIp = string.IsNullOrWhiteSpace(normalizedHost) ? input.Ip.Trim() : normalizedHost;

            var entity = new ServerPingTarget
            {
                Name = input.Name.Trim(),
                Ip = effectiveIp,
                TimeoutMs = input.TimeoutMs > 0 ? input.TimeoutMs : 2000,
                IsActive = input.IsActive,
                SortOrder = input.SortOrder,
                Description = input.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "admin",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "admin",
                IsDeleted = false,
            };

            db.ServerPingTargets.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<ServerPingTargetDto>.Ok(ToDto(entity)));
        })
        .WithName("CreateServerPingTarget");

        // 감시 대상 장비 수정
        group.MapPut("/targets/{id:int}", async (
            int id,
            [FromBody] UpdateServerPingTargetDto input,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var entity = await db.ServerPingTargets.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (entity == null)
                return Results.NotFound(ApiResponse<ServerPingTargetDto>.Fail("해당 장비를 찾을 수 없습니다."));

            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(ApiResponse<ServerPingTargetDto>.Fail("장비명을 입력해 주세요."));

            if (string.IsNullOrWhiteSpace(input.Ip))
                return Results.BadRequest(ApiResponse<ServerPingTargetDto>.Fail("IP 주소 또는 도메인을 입력해 주세요."));

            var (normalizedHost, _) = NormalizeTarget(input.Ip);
            var effectiveIp = string.IsNullOrWhiteSpace(normalizedHost) ? input.Ip.Trim() : normalizedHost;

            entity.Name = input.Name.Trim();
            entity.Ip = effectiveIp;
            entity.TimeoutMs = input.TimeoutMs > 0 ? input.TimeoutMs : 2000;
            entity.IsActive = input.IsActive;
            entity.SortOrder = input.SortOrder;
            entity.Description = input.Description?.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = "admin";

            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<ServerPingTargetDto>.Ok(ToDto(entity)));
        })
        .WithName("UpdateServerPingTarget");

        // 감시 대상 장비 삭제 (소프트 삭제)
        group.MapDelete("/targets/{id:int}", async (
            int id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var entity = await db.ServerPingTargets.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (entity == null)
                return Results.NotFound(ApiResponse<bool>.Fail("해당 장비를 찾을 수 없습니다."));

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = "admin";

            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("DeleteServerPingTarget");

        // 등록된 모든 활성 장비의 실시간 Ping 상태 일괄 조회
        group.MapGet("/targets-status", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var targets = await db.ServerPingTargets
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            // 병렬로 ping 체크
            var tasks = targets.Select(async target =>
            {
                var timeoutMs = target.TimeoutMs > 0 ? target.TimeoutMs : DefaultTimeoutMs;
                var pingResult = await PingServerAsync(target.Ip, timeoutMs, ct, target.Name);
                return new ServerPingDeviceStatusDto
                {
                    Target = ToDto(target),
                    PingResult = pingResult,
                };
            });

            var results = await Task.WhenAll(tasks);
            return Results.Ok(ApiResponse<List<ServerPingDeviceStatusDto>>.Ok(results.ToList()));
        })
        .WithName("GetAllServerPingStatus");
    }

    private static ServerPingTargetDto ToDto(ServerPingTarget entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Ip = entity.Ip,
        TimeoutMs = entity.TimeoutMs,
        IsActive = entity.IsActive,
        SortOrder = entity.SortOrder,
        Description = entity.Description,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    /// <summary>
    /// 입력된 IP 또는 도메인 주소에서 프로토콜, 경로, 포트 등을 정규화하여 호스트명과 포트를 분리한다.
    /// </summary>
    public static (string Host, int? Port) NormalizeTarget(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (string.Empty, null);
        var trimmed = input.Trim();

        // http:// 또는 https:// 접두사 제거
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(7);
        else if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(8);

        // 경로(/...) 제거
        var slashIdx = trimmed.IndexOf('/');
        if (slashIdx >= 0)
            trimmed = trimmed.Substring(0, slashIdx);

        int? port = null;
        var colonIdx = trimmed.LastIndexOf(':');
        if (colonIdx >= 0 && int.TryParse(trimmed.Substring(colonIdx + 1), out var p) && p > 0 && p <= 65535)
        {
            port = p;
            trimmed = trimmed.Substring(0, colonIdx);
        }

        return (trimmed.TrimEnd('.'), port);
    }

    internal static async Task<ServerPingResultDto> PingServerAsync(
        string targetIp, int timeoutMs, CancellationToken ct, string? deviceName = null)
    {
        var (host, customPort) = NormalizeTarget(targetIp);
        var effectiveTarget = string.IsNullOrWhiteSpace(host) ? (targetIp?.Trim() ?? string.Empty) : host;
        var name = !string.IsNullOrWhiteSpace(deviceName) ? deviceName : GetDefaultDeviceName(effectiveTarget);

        var dto = new ServerPingResultDto
        {
            TargetIp = targetIp ?? string.Empty,
            DeviceName = name,
            CheckedAt = DateTime.UtcNow,
        };

        if (string.IsNullOrWhiteSpace(effectiveTarget))
        {
            dto.Status = "Error";
            dto.Message = "IP 주소 또는 도메인을 입력해 주세요.";
            return dto;
        }

        IPAddress? resolvedIp = null;
        bool isDomain = !IPAddress.TryParse(effectiveTarget, out resolvedIp);

        // 도메인인 경우 DNS 조회 시도
        if (isDomain)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(effectiveTarget, ct);
                if (addresses.Length > 0)
                {
                    resolvedIp = addresses[0];
                }
            }
            catch (Exception ex)
            {
                dto.IsAlive = false;
                dto.Status = "DnsError";
                dto.Message = $"도메인 조회 실패 ({ex.Message})";
                return dto;
            }
        }

        var ipDisplay = isDomain && resolvedIp != null ? $" [{resolvedIp}]" : string.Empty;
        var sw = Stopwatch.StartNew();

        // 1. .NET Ping 클래스 시도
        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync(effectiveTarget, timeoutMs);
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                dto.IsAlive = true;
                dto.RoundtripTimeMs = reply.RoundtripTime;
                dto.Status = reply.Status.ToString();
                dto.Message = $"정상 응답 수신 ({reply.RoundtripTime}ms{ipDisplay})";
                return dto;
            }
        }
        catch (PingException)
        {
            // raw socket 권한 부재 또는 ICMP 차단 환경 -> Fallback으로 진행
        }
        catch (Exception)
        {
            // 기타 오류 -> Fallback으로 진행
        }

        // 2. ICMP 차단/실패 환경(클라우드 호스트 등) 대응: TCP 연결 및 Fallback 검사
        var connectTarget = resolvedIp?.ToString() ?? effectiveTarget;
        var fallback = await FallbackPingAsync(effectiveTarget, connectTarget, customPort, timeoutMs, name, ipDisplay, ct);
        return fallback;
    }

    /// <summary>
    /// Ping(ICMP) 권한이 없거나 방화벽으로 ICMP가 차단된 환경(Docker 컨테이너, 클라우드 호스트 등)을 위한 대체 생존 검사.
    /// 주요 서비스 포트로의 빠른 TCP 연결 시도로 호스트 생존 여부를 가늠한다.
    /// </summary>
    private static async Task<ServerPingResultDto> FallbackPingAsync(
        string host, string connectTarget, int? customPort, int timeoutMs, string deviceName, string ipDisplay, CancellationToken ct)
    {
        var candidatePorts = new List<int>();
        if (customPort.HasValue) candidatePorts.Add(customPort.Value);
        // 범용 웹/원격접속/DB 포트 (도메인 서버 등에서 주로 열려있는 서비스 포트)
        candidatePorts.AddRange(new[] { 80, 443, 22, 8080, 1521, 3306, 5432, 445, 139 });

        var sw = Stopwatch.StartNew();

        foreach (var port in candidatePorts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(Math.Min(timeoutMs, 500));

                var connectTask = client.ConnectAsync(connectTarget, port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(Math.Min(timeoutMs, 500), cts.Token));

                if (completed == connectTask && client.Connected)
                {
                    sw.Stop();
                    return new ServerPingResultDto
                    {
                        TargetIp = host,
                        DeviceName = deviceName,
                        IsAlive = true,
                        RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                        Status = "Success",
                        Message = $"정상 응답 수신 (TCP {port} 포트 연결됨{ipDisplay})",
                        CheckedAt = DateTime.UtcNow,
                    };
                }
            }
            catch (SocketException sex) when (sex.SocketErrorCode is SocketError.ConnectionRefused or SocketError.AccessDenied)
            {
                // 포트가 닫혀있다는 응답(RST)을 보냈다는 것은 해당 IP/도메인의 호스트가 살아있음을 의미
                sw.Stop();
                return new ServerPingResultDto
                {
                    TargetIp = host,
                    DeviceName = deviceName,
                    IsAlive = true,
                    RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                    Status = "Success",
                    Message = $"호스트 생존 확인 (포트 {port} 응답 확인됨{ipDisplay})",
                    CheckedAt = DateTime.UtcNow,
                };
            }
            catch
            {
                // 다음 포트 시도
            }
        }

        // 마지막 수단: OS ping CLI (/bin/ping) 1회 시도 (호스트명 안전 검사)
        if (Regex.IsMatch(host, @"^[a-zA-Z0-9.\-_]+$"))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ping",
                    Arguments = $"-c 1 -W {Math.Max(1, timeoutMs / 1000)} {host}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync(ct);
                    if (proc.ExitCode == 0)
                    {
                        sw.Stop();
                        return new ServerPingResultDto
                        {
                            TargetIp = host,
                            DeviceName = deviceName,
                            IsAlive = true,
                            RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                            Status = "Success",
                            Message = $"정상 응답 수신 (CLI Ping{ipDisplay})",
                            CheckedAt = DateTime.UtcNow,
                        };
                    }
                }
            }
            catch
            {
                // CLI 불가 시 무시
            }
        }

        sw.Stop();
        return new ServerPingResultDto
        {
            TargetIp = host,
            DeviceName = deviceName,
            IsAlive = false,
            RoundtripTimeMs = sw.ElapsedMilliseconds,
            Status = "Unreachable",
            Message = $"통신 불가 (응답 없음{ipDisplay})",
            CheckedAt = DateTime.UtcNow,
        };
    }
}
