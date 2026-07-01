using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 속성 관리 서비스 구현체
/// Device 1:N DeviceAttribute 관계를 관리합니다.
/// </summary>
public class DeviceAttributeService : IDeviceAttributeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceAttributeService> _logger;
    private readonly IDeviceHubSender _deviceHubSender;

    public DeviceAttributeService(AppDbContext context, ILogger<DeviceAttributeService> logger, IDeviceHubSender deviceHubSender)
    {
        _context = context;
        _logger = logger;
        _deviceHubSender = deviceHubSender;
    }

    /// <summary>
    /// 장비 ID로 속성 조회
    /// </summary>
    public async Task<DeviceAttributeDto?> GetByDeviceIdAsync(string deviceId)
    {
        _logger.LogInformation("장비 속성 조회. DeviceId: {DeviceId}", deviceId);

        var attr = await _context.DeviceAttributes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.DeviceId == deviceId);

        return attr == null ? null : MapToDto(attr);
    }

    /// <summary>
    /// 장비 속성 Upsert - 없으면 생성, 있으면 수정
    /// </summary>
    public async Task<DeviceAttributeDto> UpsertAsync(DeviceAttributeUpsertDto dto)
    {
        _logger.LogInformation("장비 속성 Upsert. DeviceId: {DeviceId}", dto.DeviceId);

        var existing = await _context.DeviceAttributes
            .FirstOrDefaultAsync(a => a.DeviceId == dto.DeviceId);

        if (existing == null)
        {
            // 신규 생성
            existing = new DeviceAttribute { DeviceId = dto.DeviceId };
            _context.DeviceAttributes.Add(existing);
            _logger.LogInformation("장비 속성 신규 생성. DeviceId: {DeviceId}", dto.DeviceId);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("장비 속성 수정. DeviceId: {DeviceId}, Id: {Id}", dto.DeviceId, existing.Id);
        }

        // 공통 표시 설정 적용
        existing.DisplayOrientation = dto.DisplayOrientation;
        existing.PortraitOrientation = dto.PortraitOrientation;
        existing.VideoOrientation = dto.VideoOrientation;
        existing.DisplayPaddingTop = dto.DisplayPaddingTop;
        existing.DisplayPaddingLeft = dto.DisplayPaddingLeft;
        existing.DisplayPaddingRight = dto.DisplayPaddingRight;
        existing.DisplayPaddingBottom = dto.DisplayPaddingBottom;
        existing.ContentIntervalSec = dto.ContentIntervalSec;
        existing.IsScreensaverEnabled = dto.IsScreensaverEnabled;
        existing.ScreensaverTimeoutSec = dto.ScreensaverTimeoutSec;

        // 영정사진/추모 콘텐츠 설정 적용
        existing.IsMemorialPhotoEnabled = dto.IsMemorialPhotoEnabled;
        existing.MemorialPhotoEffect = dto.MemorialPhotoEffect;
        existing.IsDeceasedNameVisible = dto.IsDeceasedNameVisible;
        existing.IsFamilyContactVisible = dto.IsFamilyContactVisible;
        existing.MemorialPaddingTop = dto.MemorialPaddingTop;
        existing.MemorialPaddingLeft = dto.MemorialPaddingLeft;
        existing.MemorialPaddingRight = dto.MemorialPaddingRight;
        existing.MemorialPaddingBottom = dto.MemorialPaddingBottom;

        // 멀티미디어 콘텐츠 설정 적용
        existing.IsVideoEnabled = dto.IsVideoEnabled;
        existing.IsMusicEnabled = dto.IsMusicEnabled;
        existing.VideoId = dto.VideoId;
        existing.MusicId = dto.MusicId;
        existing.MusicVolume = dto.MusicVolume;
        existing.IsMediaLoop = dto.IsMediaLoop;
        existing.IsMuted = dto.IsMuted;

        // 층별 안내 설정 적용
        existing.IsFloorGuideEnabled = dto.IsFloorGuideEnabled;
        existing.IsRoomAssignmentVisible = dto.IsRoomAssignmentVisible;
        existing.IsActiveRoomsOnly = dto.IsActiveRoomsOnly;
        existing.FloorGuideRefreshSec = dto.FloorGuideRefreshSec;

        // 입구 정보/키오스크 설정 적용
        existing.IsTouchEnabled = dto.IsTouchEnabled;
        existing.IsQrCodeVisible = dto.IsQrCodeVisible;
        existing.IsBuildingMapVisible = dto.IsBuildingMapVisible;
        existing.EntranceGreeting = dto.EntranceGreeting;
        existing.IsNoticeVisible = dto.IsNoticeVisible;
        existing.NoticeScrollSpeed = dto.NoticeScrollSpeed;

        existing.Remark = dto.Remark;

        await _context.SaveChangesAsync();

        // 실시간 변경 알림 송신
        try
        {
            await _deviceHubSender.SendDeviceChangedByDeviceIdAsync(dto.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR 장비 속성 변경 알림 전송 중 에러 발생");
        }

        return MapToDto(existing);
    }

    /// <summary>
    /// 장비 속성 삭제 (장비 삭제 시 연계 삭제용)
    /// </summary>
    public async Task<bool> DeleteByDeviceIdAsync(string deviceId)
    {
        _logger.LogInformation("장비 속성 삭제. DeviceId: {DeviceId}", deviceId);

        var attr = await _context.DeviceAttributes
            .FirstOrDefaultAsync(a => a.DeviceId == deviceId);

        if (attr == null)
        {
            _logger.LogWarning("삭제할 장비 속성이 없습니다. DeviceId: {DeviceId}", deviceId);
            return false;
        }

        _context.DeviceAttributes.Remove(attr);
        await _context.SaveChangesAsync();

        _logger.LogInformation("장비 속성 삭제 완료. DeviceId: {DeviceId}", deviceId);
        return true;
    }

    // ────────────────────────────────────────────────────────
    // private helper
    // ────────────────────────────────────────────────────────

    private static DeviceAttributeDto MapToDto(DeviceAttribute a) => new()
    {
        Id = a.Id,
        DeviceId = a.DeviceId,
        DisplayOrientation = a.DisplayOrientation,
        PortraitOrientation = a.PortraitOrientation,
        VideoOrientation = a.VideoOrientation,
        DisplayPaddingTop = a.DisplayPaddingTop,
        DisplayPaddingLeft = a.DisplayPaddingLeft,
        DisplayPaddingRight = a.DisplayPaddingRight,
        DisplayPaddingBottom = a.DisplayPaddingBottom,
        ContentIntervalSec = a.ContentIntervalSec,
        IsScreensaverEnabled = a.IsScreensaverEnabled,
        ScreensaverTimeoutSec = a.ScreensaverTimeoutSec,
        IsMemorialPhotoEnabled = a.IsMemorialPhotoEnabled,
        MemorialPhotoEffect = a.MemorialPhotoEffect,
        IsDeceasedNameVisible = a.IsDeceasedNameVisible,
        IsFamilyContactVisible = a.IsFamilyContactVisible,
        MemorialPaddingTop = a.MemorialPaddingTop,
        MemorialPaddingLeft = a.MemorialPaddingLeft,
        MemorialPaddingRight = a.MemorialPaddingRight,
        MemorialPaddingBottom = a.MemorialPaddingBottom,
        IsVideoEnabled = a.IsVideoEnabled,
        IsMusicEnabled = a.IsMusicEnabled,
        VideoId = a.VideoId,
        MusicId = a.MusicId,
        MusicVolume = a.MusicVolume,
        IsMediaLoop = a.IsMediaLoop,
        IsMuted = a.IsMuted,
        IsFloorGuideEnabled = a.IsFloorGuideEnabled,
        IsRoomAssignmentVisible = a.IsRoomAssignmentVisible,
        IsActiveRoomsOnly = a.IsActiveRoomsOnly,
        FloorGuideRefreshSec = a.FloorGuideRefreshSec,
        IsTouchEnabled = a.IsTouchEnabled,
        IsQrCodeVisible = a.IsQrCodeVisible,
        IsBuildingMapVisible = a.IsBuildingMapVisible,
        EntranceGreeting = a.EntranceGreeting,
        IsNoticeVisible = a.IsNoticeVisible,
        NoticeScrollSpeed = a.NoticeScrollSpeed,
        Remark = a.Remark,
    };
}
