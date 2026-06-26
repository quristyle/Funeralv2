using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 관리 서비스 구현체
/// </summary>
public class DeviceService : IDeviceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(AppDbContext context, ILogger<DeviceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 장비 전체 목록 조회
    /// </summary>
    public async Task<List<DeviceDto>> GetAllAsync()
    {
        _logger.LogInformation("장비 전체 목록을 조회합니다.");

        var devices = await _context.Devices
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync();

        return devices.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 필터 기반 장비 목록 조회 (회사, 건물, 층, 호실)
    /// </summary>
    public async Task<List<DeviceDto>> GetByFilterAsync(string? companyId, string? buildingId, string? floorId, string? roomId)
    {
        _logger.LogInformation(
            "필터 조건으로 장비 목록을 조회합니다. CompanyId: {CompanyId}, BuildingId: {BuildingId}, FloorId: {FloorId}, RoomId: {RoomId}",
            companyId, buildingId, floorId, roomId);

        var query = _context.Devices.AsNoTracking();

        if (!string.IsNullOrEmpty(roomId))
        {
            query = query.Where(d => d.RoomId == roomId);
        }
        else if (!string.IsNullOrEmpty(floorId))
        {
            query = query.Where(d => d.FloorId == floorId);
        }
        else if (!string.IsNullOrEmpty(buildingId))
        {
            query = query.Where(d => d.BuildingId == buildingId);
        }
        else if (!string.IsNullOrEmpty(companyId))
        {
            // Buildings와 조인하여 companyId로 필터링
            query = from device in query
                    join building in _context.Buildings on device.BuildingId equals building.Id
                    where building.CompanyId == companyId
                    select device;
        }
        else
        {
            return [];
        }

        var devices = await query
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync();
        return devices.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 단일 장비 상세 조회
    /// </summary>
    public async Task<DeviceDto?> GetByIdAsync(string id)
    {
        _logger.LogInformation("장비 상세 정보를 조회합니다. Id: {Id}", id);

        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            _logger.LogWarning("장비를 찾을 수 없습니다. Id: {Id}", id);
            return null;
        }

        return MapToDto(device);
    }

    /// <summary>
    /// 장비 생성 - 생성된 장비 ID 반환
    /// </summary>
    public async Task<string> CreateAsync(DeviceCreateDto item)
    {
        _logger.LogInformation("새 장비를 생성합니다. Name: {Name}, Code: {Code}", item.Name, item.Code);

        var entity = new Device
        {
            Name = item.Name,
            Code = item.Code,
            DeviceType = item.DeviceType,
            IpAddress = item.IpAddress,
            MacAddress = item.MacAddress,
            Status = item.Status,
            SortOrder = item.SortOrder,
            BuildingId = item.BuildingId,
            FloorId = item.FloorId,
            RoomId = item.RoomId,
        };

        _context.Devices.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("장비 생성 완료. Id: {Id}", entity.Id);
        return entity.Id;
    }

    /// <summary>
    /// 장비 수정 - 성공 여부 반환
    /// </summary>
    public async Task<bool> UpdateAsync(string id, DeviceUpdateDto item)
    {
        _logger.LogInformation("장비 정보를 수정합니다. Id: {Id}, Name: {Name}", id, item.Name);

        var entity = await _context.Devices.FindAsync(id);
        if (entity == null)
        {
            _logger.LogWarning("수정할 장비를 찾을 수 없습니다. Id: {Id}", id);
            return false;
        }

        entity.Name = item.Name;
        entity.DeviceType = item.DeviceType;
        entity.IpAddress = item.IpAddress;
        entity.MacAddress = item.MacAddress;
        entity.Status = item.Status;
        entity.SortOrder = item.SortOrder;
        entity.BuildingId = item.BuildingId;
        entity.FloorId = item.FloorId;
        entity.RoomId = item.RoomId;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation("장비 수정 완료. Id: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "장비 수정 중 오류가 발생했습니다. Id: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// 장비 삭제 - 성공 여부 반환
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("장비를 삭제합니다. Id: {Id}", id);

        var entity = await _context.Devices.FindAsync(id);
        if (entity == null)
        {
            _logger.LogWarning("삭제할 장비를 찾을 수 없습니다. Id: {Id}", id);
            return false;
        }

        try
        {
            _context.Devices.Remove(entity);
            await _context.SaveChangesAsync();
            _logger.LogInformation("장비 삭제 완료. Id: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "장비 삭제 중 오류가 발생했습니다. Id: {Id}", id);
            throw;
        }
    }

    // ────────────────────────────────────────────────────────
    // private helper
    // ────────────────────────────────────────────────────────

    private static DeviceDto MapToDto(Device d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Code = d.Code,
        DeviceType = d.DeviceType,
        IpAddress = d.IpAddress,
        MacAddress = d.MacAddress,
        Status = d.Status,
        SortOrder = d.SortOrder,
        BuildingId = d.BuildingId,
        FloorId = d.FloorId,
        RoomId = d.RoomId,
    };
}
