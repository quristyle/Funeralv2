using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using funeralv2Api.Hubs;
using funeralv2Api.Data;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 실시간 SignalR 알림 발송용 인터페이스
/// </summary>
public interface IDeviceHubSender
{
    Task SendDeviceChangedAsync(string deviceCode);
    Task SendDeviceChangedByDeviceIdAsync(string deviceId);
    Task SendDeviceChangedByRoomIdAsync(string roomId);
    Task SendDeviceStatusChangedAsync(string deviceCode, string status);

    /// <summary>
    /// 원격 모니터 전원 제어 명령을 해당 장비에 즉시 전달한다.
    /// </summary>
    Task SendScreenPowerAsync(string deviceCode, bool on);

    /// <summary>
    /// 플레이어 앱 재시작 명령을 해당 장비에 즉시 전달한다 (47번 문서 D-RS3).
    /// OS 재부팅이 아니라 앱 프로세스 재시작이다 — 리눅스는 systemd(Restart=always)가 되살린다.
    /// </summary>
    Task SendAppRestartAsync(string deviceCode);
}

/// <summary>
/// 장비 실시간 SignalR 알림 발송 구현체
/// </summary>
public class DeviceHubSender : IDeviceHubSender
{
    private readonly IHubContext<DeviceHub> _hubContext;
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceHubSender> _logger;

    public DeviceHubSender(
        IHubContext<DeviceHub> hubContext, 
        AppDbContext context, 
        ILogger<DeviceHubSender> logger)
    {
        _hubContext = hubContext;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 장비코드로 직접 SignalR 변경 이벤트 브로드캐스팅
    /// </summary>
    public async Task SendDeviceChangedAsync(string deviceCode)
    {
        if (string.IsNullOrEmpty(deviceCode)) return;
        
        await _hubContext.Clients.Group(deviceCode).SendAsync("DeviceChanged", deviceCode);
        _logger.LogInformation("SignalR [DeviceChanged] sent to group: {DeviceCode}", deviceCode);
    }

    /// <summary>
    /// 원격 모니터 전원 명령을 즉시 보낸다. 저장하지 않는다 — 꺼진 채 부팅되면 원격으로 되살릴 수단이 없다.
    /// </summary>
    public async Task SendScreenPowerAsync(string deviceCode, bool on)
    {
        if (string.IsNullOrEmpty(deviceCode)) return;

        var state = on ? "ON" : "OFF";
        await _hubContext.Clients.Group(deviceCode).SendAsync("ScreenPower", state);
        _logger.LogInformation("SignalR [ScreenPower] sent to group: {DeviceCode}, State: {State}", deviceCode, state);
    }

    /// <summary>
    /// 플레이어 앱 재시작 명령을 SignalR 로 보낸다. 저장하지 않는다.
    /// </summary>
    public async Task SendAppRestartAsync(string deviceCode)
    {
        if (string.IsNullOrEmpty(deviceCode)) return;

        await _hubContext.Clients.Group(deviceCode).SendAsync("AppRestart");
        _logger.LogInformation("SignalR [AppRestart] sent to group: {DeviceCode}", deviceCode);
    }

    /// <summary>
    /// 장비 상태 변경 정보를 모든 관리자에게 실시간 전송
    /// </summary>
    public async Task SendDeviceStatusChangedAsync(string deviceCode, string status)
    {
        if (string.IsNullOrEmpty(deviceCode)) return;
        
        await _hubContext.Clients.All.SendAsync("DeviceStatusChanged", deviceCode, status);
        _logger.LogInformation("SignalR [DeviceStatusChanged] sent. Code: {DeviceCode}, Status: {Status}", deviceCode, status);
    }

    /// <summary>
    /// 장비 ID를 조회하여 해당 장비코드로 SignalR 변경 이벤트 브로드캐스팅
    /// </summary>
    public async Task SendDeviceChangedByDeviceIdAsync(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return;

        var device = await _context.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId);
       
        _logger.LogInformation("SignalR [DeviceChanged] find: {device}", device);
       
        if (device != null)
        {
            _logger.LogInformation("SignalR [DeviceChanged] fined: {device}", device);
            await SendDeviceChangedAsync(device.Code);
        }
    }

    /// <summary>
    /// 호실에 속한 장비들에 SignalR 변경 이벤트를 보낸다. 배정이 바뀌는 자리에서만 불리므로
    /// RoomAssignmentChanged 가 곧 호실 점유 변경 신호다.
    /// </summary>
    public async Task SendDeviceChangedByRoomIdAsync(string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        var devices = await _context.Devices.AsNoTracking()
            .Where(d => d.RoomId == roomId && !d.IsDeleted)
            .Select(d => d.Code)
            .ToListAsync();

        foreach (var code in devices)
        {
            await SendDeviceChangedAsync(code);
        }

        await _hubContext.Clients.All.SendAsync("RoomAssignmentChanged", roomId);
        _logger.LogInformation("SignalR [RoomAssignmentChanged] sent. RoomId: {RoomId}", roomId);
    }
}
