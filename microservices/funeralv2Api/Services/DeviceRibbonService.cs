using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 리본 설정 서비스 구현체
/// 장비 화면에 장식(리본) 이미지를 배치하는 설정을 관리합니다.
/// </summary>
public class DeviceRibbonService : IDeviceRibbonService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceRibbonService> _logger;

    public DeviceRibbonService(AppDbContext context, ILogger<DeviceRibbonService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 장비 ID로 리본 목록 조회 (장식 이미지 정보 포함)
    /// </summary>
    public async Task<List<DeviceRibbonDto>> GetByDeviceIdAsync(string deviceId)
    {
        _logger.LogInformation("장비 리본 목록 조회. DeviceId: {DeviceId}", deviceId);

        var ribbons = await _context.DeviceRibbons
            .AsNoTracking()
            .Include(r => r.MediaSource)
            .Where(r => r.DeviceId == deviceId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();

        return ribbons.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 리본 단건 조회
    /// </summary>
    public async Task<DeviceRibbonDto?> GetByIdAsync(string id)
    {
        _logger.LogInformation("리본 단건 조회. Id: {Id}", id);

        var ribbon = await _context.DeviceRibbons
            .AsNoTracking()
            .Include(r => r.MediaSource)
            .FirstOrDefaultAsync(r => r.Id == id);

        return ribbon == null ? null : MapToDto(ribbon);
    }

    /// <summary>
    /// 리본 단건 생성
    /// </summary>
    public async Task<DeviceRibbonDto> CreateAsync(DeviceRibbonUpsertDto dto)
    {
        _logger.LogInformation("리본 생성. DeviceId: {DeviceId}, MediaSourceId: {MediaSourceId}", dto.DeviceId, dto.MediaSourceId);

        var ribbon = new DeviceRibbon
        {
            DeviceId = dto.DeviceId,
            MediaSourceId = dto.MediaSourceId,
            PositionLeft = Math.Round(dto.PositionLeft, 3),
            PositionTop = Math.Round(dto.PositionTop, 3),
            Width = Math.Round(dto.Width, 3),
            Height = Math.Round(dto.Height, 3),
            SortOrder = dto.SortOrder,
            Remark = dto.Remark,
        };

        _context.DeviceRibbons.Add(ribbon);
        await _context.SaveChangesAsync();

        // 미디어소스 정보 포함하여 반환
        await _context.Entry(ribbon).Reference(r => r.MediaSource).LoadAsync();

        _logger.LogInformation("리본 생성 완료. Id: {Id}", ribbon.Id);
        return MapToDto(ribbon);
    }

    /// <summary>
    /// 리본 단건 수정
    /// </summary>
    public async Task<DeviceRibbonDto?> UpdateAsync(string id, DeviceRibbonUpsertDto dto)
    {
        _logger.LogInformation("리본 수정. Id: {Id}", id);

        var ribbon = await _context.DeviceRibbons
            .Include(r => r.MediaSource)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (ribbon == null)
        {
            _logger.LogWarning("수정할 리본이 없습니다. Id: {Id}", id);
            return null;
        }

        ribbon.MediaSourceId = dto.MediaSourceId;
        ribbon.PositionLeft = Math.Round(dto.PositionLeft, 3);
        ribbon.PositionTop = Math.Round(dto.PositionTop, 3);
        ribbon.Width = Math.Round(dto.Width, 3);
        ribbon.Height = Math.Round(dto.Height, 3);
        ribbon.SortOrder = dto.SortOrder;
        ribbon.Remark = dto.Remark;
        ribbon.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // 미디어소스 정보 갱신
        await _context.Entry(ribbon).Reference(r => r.MediaSource).LoadAsync();

        _logger.LogInformation("리본 수정 완료. Id: {Id}", id);
        return MapToDto(ribbon);
    }

    /// <summary>
    /// 리본 단건 삭제
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("리본 삭제. Id: {Id}", id);

        var ribbon = await _context.DeviceRibbons
            .FirstOrDefaultAsync(r => r.Id == id);

        if (ribbon == null)
        {
            _logger.LogWarning("삭제할 리본이 없습니다. Id: {Id}", id);
            return false;
        }

        _context.DeviceRibbons.Remove(ribbon);
        await _context.SaveChangesAsync();

        _logger.LogInformation("리본 삭제 완료. Id: {Id}", id);
        return true;
    }

    /// <summary>
    /// 장비의 전체 리본 목록 일괄 저장
    /// 기존 리본을 모두 삭제하고 새로운 목록으로 교체합니다.
    /// </summary>
    public async Task<List<DeviceRibbonDto>> BulkSaveAsync(DeviceRibbonBulkSaveDto dto)
    {
        _logger.LogInformation("리본 일괄 저장. DeviceId: {DeviceId}, Count: {Count}", dto.DeviceId, dto.Ribbons.Count);

        // 기존 리본 모두 삭제
        var existingRibbons = await _context.DeviceRibbons
            .Where(r => r.DeviceId == dto.DeviceId)
            .ToListAsync();

        _context.DeviceRibbons.RemoveRange(existingRibbons);

        // 새로운 리본 목록 삽입
        var newRibbons = dto.Ribbons.Select((r, index) => new DeviceRibbon
        {
            DeviceId = dto.DeviceId,
            MediaSourceId = r.MediaSourceId,
            PositionLeft = Math.Round(r.PositionLeft, 3),
            PositionTop = Math.Round(r.PositionTop, 3),
            Width = Math.Round(r.Width, 3),
            Height = Math.Round(r.Height, 3),
            SortOrder = r.SortOrder > 0 ? r.SortOrder : index,
            Remark = r.Remark,
        }).ToList();

        _context.DeviceRibbons.AddRange(newRibbons);
        await _context.SaveChangesAsync();

        // 미디어소스 정보 포함하여 반환
        var result = await _context.DeviceRibbons
            .AsNoTracking()
            .Include(r => r.MediaSource)
            .Where(r => r.DeviceId == dto.DeviceId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("리본 일괄 저장 완료. DeviceId: {DeviceId}, Count: {Count}", dto.DeviceId, result.Count);
        return result.Select(MapToDto).ToList();
    }

    // ────────────────────────────────────────────────────────
    // private helper
    // ────────────────────────────────────────────────────────

    private static DeviceRibbonDto MapToDto(DeviceRibbon r) => new()
    {
        Id = r.Id,
        DeviceId = r.DeviceId,
        MediaSourceId = r.MediaSourceId,
        MediaSourceName = r.MediaSource?.Name,
        MediaSourceUrl = r.MediaSource?.Url,
        MediaSourceThumbnailUrl = r.MediaSource?.ThumbnailUrl,
        PositionLeft = r.PositionLeft,
        PositionTop = r.PositionTop,
        Width = r.Width,
        Height = r.Height,
        SortOrder = r.SortOrder,
        Remark = r.Remark,
    };
}
