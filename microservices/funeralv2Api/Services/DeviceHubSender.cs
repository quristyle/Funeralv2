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
    /// 호실 ID를 조회하여 해당 호실에 속한 모든 장비들의 코드로 SignalR 변경 이벤트 브로드캐스팅
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
    }
}
