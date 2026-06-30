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
            .Include(d => d.Building)
            .Include(d => d.Floor)
            .Include(d => d.Room)
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
            .Include(d => d.Building)
            .Include(d => d.Floor)
            .Include(d => d.Room)
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

        var device = await _context.Devices
            .Include(d => d.Building)
            .Include(d => d.Floor)
            .Include(d => d.Room)
            .FirstOrDefaultAsync(d => d.Id == id);
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
    public async Task<DeviceDto> CreateAsync(DeviceCreateDto item)
    {
        _logger.LogInformation("새 장비를 생성합니다. Name: {Name}", item.Name);

        if (string.IsNullOrEmpty(item.BuildingId) || string.IsNullOrEmpty(item.CompanyId))
        {
            throw new ArgumentException("장비 코드를 생성하려면 회사와 건물이 지정되어야 합니다.");
        }

        // 1. 건물 약어 조회
        var building = await _context.Buildings.FindAsync(item.BuildingId);
        if (building == null || string.IsNullOrEmpty(building.Abbreviation))
        {
            throw new InvalidOperationException("건물을 찾을 수 없거나 건물 약어가 설정되지 않았습니다.");
        }
        var buildingAbbr = building.Abbreviation;

        // 2. 생성 월(MM)
        var creationMonth = DateTime.UtcNow.ToString("MM");

        // 3. 회사별 장비 인덱스 계산
        var deviceCountInCompany = await _context.Devices.CountAsync(d => d.CompanyId == item.CompanyId);
        var nextIndex = (deviceCountInCompany + 1).ToString("D4");

        // 4. 코드 조합: [건물약어]-[생성월]-[4자리인덱스]
        var generatedCode = $"{buildingAbbr}-{creationMonth}-{nextIndex}";
        
        var entity = new Device
        {
            Name = item.Name,
            ShortName = item.ShortName,
            Code = generatedCode,
            DeviceType = item.DeviceType,
            IpAddress = item.IpAddress,
            MacAddress = item.MacAddress,
            Status = item.Status,
            SortOrder = item.SortOrder,
            BuildingId = item.BuildingId,
            FloorId = item.FloorId,
            RoomId = item.RoomId,
            CompanyId = item.CompanyId,
        };

        _context.Devices.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("장비 생성 완료. Id: {Id}, Code: {Code}", entity.Id, entity.Code);
        return MapToDto(entity);
    }

    /// <summary>
    /// 장비 수정 - 성공 여부 반환
    /// </summary>
    public async Task<DeviceDto?> UpdateAsync(string id, DeviceUpdateDto item)
    {
        _logger.LogInformation("장비 정보를 수정합니다. Id: {Id}", id);

        var entity = await _context.Devices.FindAsync(id);
        if (entity == null)
        {
            return null;
        }

        entity.Name = item.Name;
        entity.ShortName = item.ShortName;
        entity.DeviceType = item.DeviceType;
        entity.IpAddress = item.IpAddress;
        entity.MacAddress = item.MacAddress;
        entity.Status = item.Status;
        entity.SortOrder = item.SortOrder;
        entity.BuildingId = item.BuildingId;
        entity.FloorId = item.FloorId;
        entity.RoomId = item.RoomId;
        entity.CompanyId = item.CompanyId;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation("장비 수정 완료. Id: {Id}", id);
            return MapToDto(entity);
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
        ShortName = d.ShortName,
        Code = d.Code,
        DeviceType = d.DeviceType,
        IpAddress = d.IpAddress,
        MacAddress = d.MacAddress,
        Status = d.Status,
        SortOrder = d.SortOrder,
        BuildingId = d.BuildingId,
        FloorId = d.FloorId,
        RoomId = d.RoomId,
        CompanyId = d.CompanyId,
        BuildingName = d.Building?.Name,
        FloorName = d.Floor?.Name,
        RoomName = d.Room?.Name,
        BuildingShortName = d.Building?.ShortName ?? d.Building?.Name,
        FloorShortName = d.Floor?.Name,
        RoomShortName = d.Room?.ShortName ?? d.Room?.Name,
    };
}
