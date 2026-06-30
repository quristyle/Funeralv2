using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace funeralv2Api.Hubs;

/// <summary>
/// 디지털 사이니지 플레이어용 실시간 통신 SignalR Hub
/// </summary>
public class DeviceHub : Hub
{
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(ILogger<DeviceHub> _logger)
    {
        this._logger = _logger;
    }

    /// <summary>
    /// 장비가 특정 장비코드를 식별자로 그룹에 참여합니다.
    /// </summary>
    public async Task RegisterDevice(string deviceCode)
    {
        if (string.IsNullOrEmpty(deviceCode))
        {
            _logger.LogWarning("빈 장비코드 수신. ConnectionId: {ConnectionId}", Context.ConnectionId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, deviceCode);
        _logger.LogInformation("장비 등록 성공: {DeviceCode} (ConnectionId: {ConnectionId})", deviceCode, Context.ConnectionId);
        
        // 연결 성공 환영 메시지 송신
        await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Welcome! Registered as {deviceCode}");
    }

    /// <summary>
    /// 장비가 그룹에서 퇴장합니다.
    /// </summary>
    public async Task UnregisterDevice(string deviceCode)
    {
        if (!string.IsNullOrEmpty(deviceCode))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, deviceCode);
            _logger.LogInformation("장비 해제 성공: {DeviceCode} (ConnectionId: {ConnectionId})", deviceCode, Context.ConnectionId);
        }
    }
}
