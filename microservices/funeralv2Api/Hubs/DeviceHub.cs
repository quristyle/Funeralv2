using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using funeralv2Api.Services;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace funeralv2Api.Hubs;

/// <summary>
/// 디지털 사이니지 플레이어용 실시간 통신 SignalR Hub
/// </summary>
public class DeviceHub : Hub
{
    private readonly ILogger<DeviceHub> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // ConnectionId와 DeviceCode 간의 매핑을 저장하는 ConcurrentDictionary
    private static readonly ConcurrentDictionary<string, string> _connectionDeviceMap = new();
    
    // DeviceCode와 오프라인 대기 스케줄러(Cancellation Token) 간의 매핑
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _disconnectTimeouts = new();

    public DeviceHub(ILogger<DeviceHub> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 장비가 특정 장비코드를 식별자로 그룹에 참여하고 온라인 상태로 갱신합니다.
    /// </summary>
    public async Task RegisterDevice(string deviceCode, string? ipAddress = null, string? macAddress = null, string? publicIpAddress = null)
    {
        if (string.IsNullOrEmpty(deviceCode))
        {
            _logger.LogWarning("빈 장비코드 수신. ConnectionId: {ConnectionId}", Context.ConnectionId);
            return;
        }

        // publicIpAddress가 명시되지 않았거나 루프백인 경우, 소켓 연결 HttpContext 정보로부터 클라이언트 공인 IP 추출 시도
        var clientPublicIp = publicIpAddress;
        if (string.IsNullOrEmpty(clientPublicIp) || clientPublicIp == "::1" || clientPublicIp == "127.0.0.1")
        {
            clientPublicIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        }

        // 동일 연결(ConnectionId)에서 장비코드가 교체된 경우 (예: JS-06-0001 -> JS-06-0005)
        if (_connectionDeviceMap.TryGetValue(Context.ConnectionId, out var oldDeviceCode))
        {
            if (oldDeviceCode != deviceCode)
            {
                _logger.LogInformation("동일 연결에서 장비코드 변경 감지. Old: {OldCode} -> New: {NewCode}", oldDeviceCode, deviceCode);

                // 1. 기존 장비코드 그룹 퇴장
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldDeviceCode);

                // 2. 기존 장비코드의 대기 중인 오프라인 타이머 해제
                if (_disconnectTimeouts.TryRemove(oldDeviceCode, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                // 3. 기존 장비는 즉시 OFFLINE으로 상태 갱신
                using (var scope = _scopeFactory.CreateScope())
                {
                    var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
                    await deviceService.UpdateStatusAsync(oldDeviceCode, "OFFLINE");
                }
            }
        }

        // 1. ConnectionId와 DeviceCode 연결 등록
        _connectionDeviceMap[Context.ConnectionId] = deviceCode;

        // 2. 만약 이 장비에 대해 대기 중인 오프라인 타이머가 있으면 취소 (재접속 감지)
        if (_disconnectTimeouts.TryRemove(deviceCode, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogInformation("장비 재접속 감지: 오프라인 타이머 취소됨. Code: {DeviceCode}", deviceCode);
        }

        // 3. SignalR 그룹 등록
        await Groups.AddToGroupAsync(Context.ConnectionId, deviceCode);

        // 4. DB 상태 업데이트 (Scoped Service 사용, IP/MAC/PublicIP 주소 포함)
        using (var scope = _scopeFactory.CreateScope())
        {
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            await deviceService.UpdateStatusAsync(deviceCode, "ONLINE", ipAddress, macAddress, clientPublicIp);
        }

        _logger.LogInformation("장비 등록 성공 (ONLINE): {DeviceCode} (ConnectionId: {ConnectionId}, LocalIP: {IP}, MAC: {MAC}, PublicIP: {PublicIP})", deviceCode, Context.ConnectionId, ipAddress, macAddress, clientPublicIp);
        await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Welcome! Registered as {deviceCode} (ONLINE)");
    }

    /// <summary>
    /// 장비가 그룹에서 퇴장하고 즉시 오프라인 처리합니다.
    /// </summary>
    public async Task UnregisterDevice(string deviceCode)
    {
        if (string.IsNullOrEmpty(deviceCode)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, deviceCode);
        _connectionDeviceMap.TryRemove(Context.ConnectionId, out _);

        // 사용자가 명시적으로 퇴장할 때(Unregister)는 즉시 OFFLINE 처리
        if (_disconnectTimeouts.TryRemove(deviceCode, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            await deviceService.UpdateStatusAsync(deviceCode, "OFFLINE");
        }

        _logger.LogInformation("장비 명시적 등록 해제 (OFFLINE): {DeviceCode}", deviceCode);
    }

    /// <summary>
    /// 연결 유실 시 유예시간 대기 후 오프라인 상태로 갱신
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionDeviceMap.TryRemove(Context.ConnectionId, out var deviceCode))
        {
            _logger.LogInformation("장비 연결 단절 감지. 유예 타이머 기동 (30초). Code: {DeviceCode}", deviceCode);

            var cts = new CancellationTokenSource();
            _disconnectTimeouts[deviceCode] = cts;

            // 백그라운드 유예시간 타이머 기동
            _ = Task.Run(async () =>
            {
                try
                {
                    // 30초 대기 (유예 기간)
                    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);

                    // 30초 경과 시점에도 딕셔너리에 대기 중인 항목이 있다면 (중간에 Cancel되지 않았다면)
                    if (_disconnectTimeouts.TryRemove(deviceCode, out _))
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
                            await deviceService.UpdateStatusAsync(deviceCode, "OFFLINE");
                        }
                        _logger.LogWarning("장비 오프라인 최종 판정: {DeviceCode}", deviceCode);
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("장비 오프라인 타이머 취소됨. Code: {DeviceCode}", deviceCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "장비 오프라인 처리 중 에러 발생. Code: {DeviceCode}", deviceCode);
                }
                finally
                {
                    cts.Dispose();
                }
            });
        }

        await base.OnDisconnectedAsync(exception);
    }
}
