using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
                return Results.BadRequest(ApiResponse<ServerPingTargetDto>.Fail("IP 주소를 입력해 주세요."));

            var entity = new ServerPingTarget
            {
                Name = input.Name.Trim(),
                Ip = input.Ip.Trim(),
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
                return Results.BadRequest(ApiResponse<ServerPingTargetDto>.Fail("IP 주소를 입력해 주세요."));

            entity.Name = input.Name.Trim();
            entity.Ip = input.Ip.Trim();
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

    internal static async Task<ServerPingResultDto> PingServerAsync(
        string targetIp, int timeoutMs, CancellationToken ct, string? deviceName = null)
    {
        var name = !string.IsNullOrWhiteSpace(deviceName) ? deviceName : GetDefaultDeviceName(targetIp);
        var dto = new ServerPingResultDto
        {
            TargetIp = targetIp,
            DeviceName = name,
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
                dto.Message = $"정상 응답 수신 ({reply.RoundtripTime}ms)";
                return dto;
            }
            else
            {
                dto.IsAlive = false;
                dto.Message = $"응답 실패: {reply.Status}";
            }
        }
        catch (PingException pex)
        {
            sw.Stop();
            // Linux 컨테이너 등 raw socket 권한 부재(Operation not permitted) 환경 대응
            return await FallbackPingAsync(targetIp, timeoutMs, pex.InnerException?.Message ?? pex.Message, name, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return await FallbackPingAsync(targetIp, timeoutMs, ex.Message, name, ct);
        }

        return dto;
    }

    /// <summary>
    /// Ping 권한이 없는 환경(Docker 컨테이너 등)을 위한 대체 생존 검사.
    /// 일반적인 열린 포트(80, 443, 22, 445 등)로의 빠른 TCP 연결 시도로 생존 여부를 가늠한다.
    /// </summary>
    private static async Task<ServerPingResultDto> FallbackPingAsync(
        string targetIp, int timeoutMs, string originalError, string deviceName, CancellationToken ct)
    {
        var candidatePorts = new[] { 80, 443, 22, 445, 139, 8080 };
        var sw = Stopwatch.StartNew();

        foreach (var port in candidatePorts)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(Math.Min(timeoutMs, 500));

                var connectTask = client.ConnectAsync(targetIp, port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(Math.Min(timeoutMs, 500), cts.Token));

                if (completed == connectTask && client.Connected)
                {
                    sw.Stop();
                    return new ServerPingResultDto
                    {
                        TargetIp = targetIp,
                        DeviceName = deviceName,
                        IsAlive = true,
                        RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                        Status = "Success",
                        Message = $"정상 응답 수신 (TCP {port} 포트 연결됨)",
                        CheckedAt = DateTime.UtcNow,
                    };
                }
            }
            catch (SocketException sex) when (sex.SocketErrorCode is SocketError.ConnectionRefused or SocketError.AccessDenied)
            {
                // 포트가 닫혀있다는 응답(RST)을 보냈다는 것은 해당 IP의 호스트가 살아있음을 의미
                sw.Stop();
                return new ServerPingResultDto
                {
                    TargetIp = targetIp,
                    DeviceName = deviceName,
                    IsAlive = true,
                    RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                    Status = "Success",
                    Message = $"호스트 생존 확인 (포트 {port} 응답 확인됨)",
                    CheckedAt = DateTime.UtcNow,
                };
            }
            catch
            {
                // 다음 포트 시도
            }
        }

        // 마지막 수단: OS ping CLI (/bin/ping) 1회 시도
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ping",
                Arguments = $"-c 1 -W {Math.Max(1, timeoutMs / 1000)} {targetIp}",
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
                        TargetIp = targetIp,
                        DeviceName = deviceName,
                        IsAlive = true,
                        RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                        Status = "Success",
                        Message = "정상 응답 수신 (CLI Ping)",
                        CheckedAt = DateTime.UtcNow,
                    };
                }
            }
        }
        catch
        {
            // CLI도 불가하면 실패 처리
        }

        sw.Stop();
        return new ServerPingResultDto
        {
            TargetIp = targetIp,
            DeviceName = deviceName,
            IsAlive = false,
            RoundtripTimeMs = sw.ElapsedMilliseconds,
            Status = "Unreachable",
            Message = $"통신 불가 ({originalError})",
            CheckedAt = DateTime.UtcNow,
        };
    }
}
