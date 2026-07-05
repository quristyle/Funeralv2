using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 텍스트 오버레이 서비스 구현체
/// 장비 화면에 텍스트를 배치하는 설정을 관리합니다.
/// </summary>
public class DeviceTextOverlayService : IDeviceTextOverlayService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceTextOverlayService> _logger;
    private readonly IDeviceHubSender _deviceHubSender;

    public DeviceTextOverlayService(AppDbContext context, ILogger<DeviceTextOverlayService> logger, IDeviceHubSender deviceHubSender)
    {
        _context = context;
        _logger = logger;
        _deviceHubSender = deviceHubSender;
    }

    /// <summary>장비 ID로 텍스트 오버레이 목록 조회</summary>
    public async Task<List<DeviceTextOverlayDto>> GetByDeviceIdAsync(string deviceId)
    {
        _logger.LogInformation("장비 텍스트 오버레이 목록 조회. DeviceId: {DeviceId}", deviceId);

        var overlays = await _context.DeviceTextOverlays
            .AsNoTracking()
            .Where(o => o.DeviceId == deviceId)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.CreatedAt)
            .ToListAsync();

        return overlays.Select(MapToDto).ToList();
    }

    /// <summary>텍스트 오버레이 단건 조회</summary>
    public async Task<DeviceTextOverlayDto?> GetByIdAsync(string id)
    {
        _logger.LogInformation("텍스트 오버레이 단건 조회. Id: {Id}", id);

        var overlay = await _context.DeviceTextOverlays
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        return overlay == null ? null : MapToDto(overlay);
    }

    /// <summary>텍스트 오버레이 단건 생성</summary>
    public async Task<DeviceTextOverlayDto> CreateAsync(DeviceTextOverlayUpsertDto dto)
    {
        _logger.LogInformation("텍스트 오버레이 생성. DeviceId: {DeviceId}", dto.DeviceId);

        var overlay = new DeviceTextOverlay
        {
            DeviceId = dto.DeviceId,
            TextContent = dto.TextContent,
            FontSize = Math.Round(dto.FontSize, 3),
            FontColor = dto.FontColor,
            BackgroundColor = dto.BackgroundColor,
            TextAlign = dto.TextAlign,
            FontWeight = dto.FontWeight,
            PositionLeft = Math.Round(dto.PositionLeft, 3),
            PositionTop = Math.Round(dto.PositionTop, 3),
            Width = Math.Round(dto.Width, 3),
            Height = Math.Round(dto.Height, 3),
            SortOrder = dto.SortOrder,
            Remark = dto.Remark,
        };

        _context.DeviceTextOverlays.Add(overlay);
        await _context.SaveChangesAsync();

        _logger.LogInformation("텍스트 오버레이 생성 완료. Id: {Id}", overlay.Id);
        return MapToDto(overlay);
    }

    /// <summary>텍스트 오버레이 단건 수정</summary>
    public async Task<DeviceTextOverlayDto?> UpdateAsync(string id, DeviceTextOverlayUpsertDto dto)
    {
        _logger.LogInformation("텍스트 오버레이 수정. Id: {Id}", id);

        var overlay = await _context.DeviceTextOverlays
            .FirstOrDefaultAsync(o => o.Id == id);

        if (overlay == null)
        {
            _logger.LogWarning("수정할 텍스트 오버레이가 없습니다. Id: {Id}", id);
            return null;
        }

        overlay.TextContent = dto.TextContent;
        overlay.FontSize = Math.Round(dto.FontSize, 3);
        overlay.FontColor = dto.FontColor;
        overlay.BackgroundColor = dto.BackgroundColor;
        overlay.TextAlign = dto.TextAlign;
        overlay.FontWeight = dto.FontWeight;
        overlay.PositionLeft = Math.Round(dto.PositionLeft, 3);
        overlay.PositionTop = Math.Round(dto.PositionTop, 3);
        overlay.Width = Math.Round(dto.Width, 3);
        overlay.Height = Math.Round(dto.Height, 3);
        overlay.SortOrder = dto.SortOrder;
        overlay.Remark = dto.Remark;
        overlay.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("텍스트 오버레이 수정 완료. Id: {Id}", id);
        return MapToDto(overlay);
    }

    /// <summary>텍스트 오버레이 단건 삭제</summary>
    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("텍스트 오버레이 삭제. Id: {Id}", id);

        var overlay = await _context.DeviceTextOverlays
            .FirstOrDefaultAsync(o => o.Id == id);

        if (overlay == null)
        {
            _logger.LogWarning("삭제할 텍스트 오버레이가 없습니다. Id: {Id}", id);
            return false;
        }

        _context.DeviceTextOverlays.Remove(overlay);
        await _context.SaveChangesAsync();

        _logger.LogInformation("텍스트 오버레이 삭제 완료. Id: {Id}", id);
        return true;
    }

    /// <summary>장비의 전체 텍스트 오버레이 일괄 저장 (전체 교체)</summary>
    public async Task<List<DeviceTextOverlayDto>> BulkSaveAsync(DeviceTextOverlayBulkSaveDto dto)
    {
        _logger.LogInformation("텍스트 오버레이 일괄 저장. DeviceId: {DeviceId}, Count: {Count}", dto.DeviceId, dto.Overlays.Count);

        // 기존 오버레이 모두 삭제
        var existing = await _context.DeviceTextOverlays
            .Where(o => o.DeviceId == dto.DeviceId)
            .ToListAsync();

        _context.DeviceTextOverlays.RemoveRange(existing);

        // 새로운 목록 삽입
        var newOverlays = dto.Overlays.Select((o, index) => new DeviceTextOverlay
        {
            DeviceId = dto.DeviceId,
            TextContent = o.TextContent,
            FontSize = Math.Round(o.FontSize, 3),
            FontColor = o.FontColor,
            BackgroundColor = o.BackgroundColor,
            TextAlign = o.TextAlign,
            FontWeight = o.FontWeight,
            PositionLeft = Math.Round(o.PositionLeft, 3),
            PositionTop = Math.Round(o.PositionTop, 3),
            Width = Math.Round(o.Width, 3),
            Height = Math.Round(o.Height, 3),
            SortOrder = o.SortOrder > 0 ? o.SortOrder : index,
            Remark = o.Remark,
        }).ToList();

        _context.DeviceTextOverlays.AddRange(newOverlays);
        await _context.SaveChangesAsync();

        var result = await _context.DeviceTextOverlays
            .AsNoTracking()
            .Where(o => o.DeviceId == dto.DeviceId)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("텍스트 오버레이 일괄 저장 완료. DeviceId: {DeviceId}, Count: {Count}", dto.DeviceId, result.Count);

        // 실시간 변경 알림 송신
        try
        {
            await _deviceHubSender.SendDeviceChangedByDeviceIdAsync(dto.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR 텍스트 오버레이 변경 알림 전송 중 에러 발생");
        }

        return result.Select(MapToDto).ToList();
    }

    // ────────────────────────────────────────────────────────
    // private helper
    // ────────────────────────────────────────────────────────

    private static DeviceTextOverlayDto MapToDto(DeviceTextOverlay o) => new()
    {
        Id = o.Id,
        DeviceId = o.DeviceId,
        TextContent = o.TextContent,
        FontSize = o.FontSize,
        FontColor = o.FontColor,
        BackgroundColor = o.BackgroundColor,
        TextAlign = o.TextAlign,
        FontWeight = o.FontWeight,
        PositionLeft = o.PositionLeft,
        PositionTop = o.PositionTop,
        Width = o.Width,
        Height = o.Height,
        SortOrder = o.SortOrder,
        Remark = o.Remark,
    };
}
