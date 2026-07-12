using funeralv2Api.Data;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <summary>
/// 백그라운드 서비스: last_seen_at 기준으로 장기 미응답 ONLINE 장비를 주기적으로 OFFLINE 처리합니다.
/// 장비 또는 서버가 갑작스럽게 종료되어 SignalR 정상 종료 흐름이 실행되지 못한 경우를 보완합니다.
/// </summary>
public class DeviceStatusCleanupService : BackgroundService
{
    /// <summary>
    /// ONLINE 상태의 장비가 이 시간(분) 이상 last_seen_at 갱신이 없으면 OFFLINE으로 전환합니다.
    /// </summary>
    private const int OFFLINE_THRESHOLD_MINUTES = 5;

    /// <summary>
    /// 장비 상태 점검 주기 (분)
    /// </summary>
    private const int CHECK_INTERVAL_MINUTES = 2;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceStatusCleanupService> _logger;

    public DeviceStatusCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceStatusCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[DeviceStatusCleanupService] 서비스 시작. 점검 주기: {Interval}분, 타임아웃 임계값: {Threshold}분",
            CHECK_INTERVAL_MINUTES,
            OFFLINE_THRESHOLD_MINUTES);

        // 첫 번째 점검은 서버 기동 직후 10초 뒤 실행 (앱 초기화 완료 대기)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndMarkStaleDevicesOfflineAsync();
            }
            catch (OperationCanceledException)
            {
                // 정상 종료 시 예외 무시
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DeviceStatusCleanupService] 장비 상태 정리 중 예외 발생");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(CHECK_INTERVAL_MINUTES), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("[DeviceStatusCleanupService] 서비스 종료.");
    }

    /// <summary>
    /// last_seen_at 기준으로 응답이 끊긴 ONLINE 장비를 탐지하여 OFFLINE 처리합니다.
    /// </summary>
    private async Task CheckAndMarkStaleDevicesOfflineAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hubSender = scope.ServiceProvider.GetRequiredService<IDeviceHubSender>();

        // 기준 시각: 현재 시각에서 임계값(분)을 뺀 시점
        var threshold = DateTime.UtcNow.AddMinutes(-OFFLINE_THRESHOLD_MINUTES);

        // last_seen_at이 null이거나 임계값보다 오래된 ONLINE 장비 조회
        var staleDevices = await db.Devices
            .Where(d => d.Status == "ONLINE"
                     && !d.IsDeleted
                     && (d.LastSeenAt == null || d.LastSeenAt < threshold))
            .ToListAsync();

        if (staleDevices.Count == 0)
        {
            _logger.LogDebug(
                "[DeviceStatusCleanupService] 타임아웃 장비 없음. (임계값: {Threshold}분 이상 미응답)",
                OFFLINE_THRESHOLD_MINUTES);
            return;
        }

        _logger.LogWarning(
            "[DeviceStatusCleanupService] 응답 없는 ONLINE 장비 {Count}개 감지. OFFLINE 전환 시작.",
            staleDevices.Count);

        foreach (var device in staleDevices)
        {
            device.Status = "OFFLINE";
            device.UpdatedAt = DateTime.UtcNow;

            _logger.LogWarning(
                "[DeviceStatusCleanupService] OFFLINE 자동 전환: Code={Code}, LastSeenAt={LastSeenAt}",
                device.Code,
                device.LastSeenAt?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "null (미기록)");

            // SignalR을 통해 관리자 화면에 실시간 상태 변경 알림
            try
            {
                await hubSender.SendDeviceStatusChangedAsync(device.Code, "OFFLINE");
                await hubSender.SendDeviceChangedByDeviceIdAsync(device.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[DeviceStatusCleanupService] SignalR 알림 전송 실패. Code: {Code}",
                    device.Code);
                // 알림 실패는 무시하고 DB 업데이트는 계속 진행
            }
        }

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[DeviceStatusCleanupService] OFFLINE 처리 완료. 총 {Count}개 장비 갱신됨.",
            staleDevices.Count);
    }
}
