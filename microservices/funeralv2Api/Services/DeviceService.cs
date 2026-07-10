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
    private readonly IDeviceHubSender _deviceHubSender;

    public DeviceService(AppDbContext context, ILogger<DeviceService> logger, IDeviceHubSender deviceHubSender)
    {
        _context = context;
        _logger = logger;
        _deviceHubSender = deviceHubSender;
    }

    /// <summary>
    /// 장비 전체 목록 조회
    /// </summary>
    public async Task<List<DeviceDto>> GetAllAsync()
    {
        _logger.LogInformation("모든 장비 목록을 조회합니다.");

        var devices = await _context.Devices
            .AsNoTracking()
            .Include(d => d.Building)
            .Include(d => d.Floor)
            .Include(d => d.Room)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync();

        var deviceIds = devices.Select(d => d.Id).ToList();
        var attributes = await _context.DeviceAttributes
            .Where(a => deviceIds.Contains(a.DeviceId) && !a.IsDeleted)
            .ToListAsync();

        var videoIds = attributes.Where(a => !string.IsNullOrEmpty(a.VideoId)).Select(a => a.VideoId!).Distinct().ToList();
        var musicIds = attributes.Where(a => !string.IsNullOrEmpty(a.MusicId)).Select(a => a.MusicId!).Distinct().ToList();
        var mediaIds = videoIds.Concat(musicIds).Distinct().ToList();
        
        var mediaMap = await _context.MediaSources
            .Where(m => mediaIds.Contains(m.Id) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.Id, m => m.ShortName ?? m.Name);

        return devices.Select(d => {
            var dto = MapToDto(d);
            var attr = attributes.FirstOrDefault(a => a.DeviceId == d.Id);
            if (attr != null)
            {
                dto.VideoId = attr.VideoId;
                dto.MusicId = attr.MusicId;
                dto.IsVideoEnabled = attr.IsVideoEnabled;
                dto.IsMusicEnabled = attr.IsMusicEnabled;
                dto.VideoName = !string.IsNullOrEmpty(attr.VideoId) && mediaMap.TryGetValue(attr.VideoId, out var vName) ? vName : null;
                dto.MusicName = !string.IsNullOrEmpty(attr.MusicId) && mediaMap.TryGetValue(attr.MusicId, out var mName) ? mName : null;
            }
            return dto;
        }).ToList();
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

        var deviceIds = devices.Select(d => d.Id).ToList();
        var attributes = await _context.DeviceAttributes
            .Where(a => deviceIds.Contains(a.DeviceId) && !a.IsDeleted)
            .ToListAsync();

        var videoIds = attributes.Where(a => !string.IsNullOrEmpty(a.VideoId)).Select(a => a.VideoId!).Distinct().ToList();
        var musicIds = attributes.Where(a => !string.IsNullOrEmpty(a.MusicId)).Select(a => a.MusicId!).Distinct().ToList();
        var mediaIds = videoIds.Concat(musicIds).Distinct().ToList();
        
        var mediaMap = await _context.MediaSources
            .Where(m => mediaIds.Contains(m.Id) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.Id, m => m.ShortName ?? m.Name);

        return devices.Select(d => {
            var dto = MapToDto(d);
            var attr = attributes.FirstOrDefault(a => a.DeviceId == d.Id);
            if (attr != null)
            {
                dto.VideoId = attr.VideoId;
                dto.MusicId = attr.MusicId;
                dto.IsVideoEnabled = attr.IsVideoEnabled;
                dto.IsMusicEnabled = attr.IsMusicEnabled;
                dto.IsMemorialPhotoEnabled = attr.IsMemorialPhotoEnabled;
                dto.IsDeceasedNameVisible = attr.IsDeceasedNameVisible;
                dto.IsFamilyContactVisible = attr.IsFamilyContactVisible;
                dto.MusicVolume = attr.MusicVolume ?? 50;
                dto.DisplayOrientation = attr.DisplayOrientation;
                dto.PortraitOrientation = attr.PortraitOrientation ?? "HORIZONTAL";
                dto.VideoOrientation = attr.VideoOrientation ?? "HORIZONTAL";
                dto.MemorialPhotoEffect = attr.MemorialPhotoEffect;
                dto.ContentIntervalSec = attr.ContentIntervalSec;
                dto.VideoName = !string.IsNullOrEmpty(attr.VideoId) && mediaMap.TryGetValue(attr.VideoId, out var vName) ? vName : null;
                dto.MusicName = !string.IsNullOrEmpty(attr.MusicId) && mediaMap.TryGetValue(attr.MusicId, out var mName) ? mName : null;
            }
            return dto;
        }).ToList();
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

        var dto = MapToDto(device);
        
        var attr = await _context.DeviceAttributes
            .FirstOrDefaultAsync(a => a.DeviceId == id && !a.IsDeleted);
        if (attr != null)
        {
            dto.VideoId = attr.VideoId;
            dto.MusicId = attr.MusicId;
            dto.IsVideoEnabled = attr.IsVideoEnabled;
            dto.IsMusicEnabled = attr.IsMusicEnabled;
            dto.IsMemorialPhotoEnabled = attr.IsMemorialPhotoEnabled;
            dto.IsDeceasedNameVisible = attr.IsDeceasedNameVisible;
            dto.IsFamilyContactVisible = attr.IsFamilyContactVisible;
            dto.MusicVolume = attr.MusicVolume ?? 50;
            dto.DisplayOrientation = attr.DisplayOrientation;
            dto.PortraitOrientation = attr.PortraitOrientation ?? "HORIZONTAL";
            dto.VideoOrientation = attr.VideoOrientation ?? "HORIZONTAL";
            dto.DisplayPaddingTop = attr.DisplayPaddingTop ?? 0;
            dto.DisplayPaddingLeft = attr.DisplayPaddingLeft ?? 0;
            dto.DisplayPaddingRight = attr.DisplayPaddingRight ?? 0;
            dto.DisplayPaddingBottom = attr.DisplayPaddingBottom ?? 0;
            dto.MemorialPaddingTop = attr.MemorialPaddingTop ?? 0;
            dto.MemorialPaddingLeft = attr.MemorialPaddingLeft ?? 0;
            dto.MemorialPaddingRight = attr.MemorialPaddingRight ?? 0;
            dto.MemorialPaddingBottom = attr.MemorialPaddingBottom ?? 0;
            dto.PhotoVerticalAlignment = attr.PhotoVerticalAlignment;
            dto.PhotoHorizontalAlignment = attr.PhotoHorizontalAlignment;
            dto.IsMuted = attr.IsMuted;
            dto.MemorialPhotoEffect = attr.MemorialPhotoEffect;
            dto.ContentIntervalSec = attr.ContentIntervalSec;
            
            if (!string.IsNullOrEmpty(attr.VideoId))
            {
                var video = await _context.MediaSources.FindAsync(attr.VideoId);
                dto.VideoName = video?.ShortName ?? video?.Name;
            }
            if (!string.IsNullOrEmpty(attr.MusicId))
            {
                var music = await _context.MediaSources.FindAsync(attr.MusicId);
                dto.MusicName = music?.ShortName ?? music?.Name;
            }
        }

        return dto;
    }

    /// <summary>
    /// 장비 코드로 상세 조회
    /// </summary>
    public async Task<DeviceDto?> GetByCodeAsync(string code)
    {
        _logger.LogInformation("장비 코드로 상세 정보를 조회합니다. Code: {Code}", code);

        var device = await _context.Devices
            .Include(d => d.Building)
            .Include(d => d.Floor)
            .Include(d => d.Room)
            .FirstOrDefaultAsync(d => d.Code == code && !d.IsDeleted);
            
        if (device == null)
        {
            _logger.LogWarning("장비를 찾을 수 없습니다. Code: {Code}", code);
            return null;
        }

        var dto = MapToDto(device);
        
        var attr = await _context.DeviceAttributes
            .FirstOrDefaultAsync(a => a.DeviceId == device.Id && !a.IsDeleted);
        if (attr != null)
        {
            dto.VideoId = attr.VideoId;
            dto.MusicId = attr.MusicId;
            dto.IsVideoEnabled = attr.IsVideoEnabled;
            dto.IsMusicEnabled = attr.IsMusicEnabled;
            dto.IsMemorialPhotoEnabled = attr.IsMemorialPhotoEnabled;
            dto.IsDeceasedNameVisible = attr.IsDeceasedNameVisible;
            dto.IsFamilyContactVisible = attr.IsFamilyContactVisible;
            dto.MusicVolume = attr.MusicVolume ?? 50;
            dto.DisplayOrientation = attr.DisplayOrientation;
            dto.PortraitOrientation = attr.PortraitOrientation ?? "HORIZONTAL";
            dto.VideoOrientation = attr.VideoOrientation ?? "HORIZONTAL";
            dto.DisplayPaddingTop = attr.DisplayPaddingTop ?? 0;
            dto.DisplayPaddingLeft = attr.DisplayPaddingLeft ?? 0;
            dto.DisplayPaddingRight = attr.DisplayPaddingRight ?? 0;
            dto.DisplayPaddingBottom = attr.DisplayPaddingBottom ?? 0;
            dto.MemorialPaddingTop = attr.MemorialPaddingTop ?? 0;
            dto.MemorialPaddingLeft = attr.MemorialPaddingLeft ?? 0;
            dto.MemorialPaddingRight = attr.MemorialPaddingRight ?? 0;
            dto.MemorialPaddingBottom = attr.MemorialPaddingBottom ?? 0;
            dto.PhotoVerticalAlignment = attr.PhotoVerticalAlignment;
            dto.PhotoHorizontalAlignment = attr.PhotoHorizontalAlignment;
            dto.IsMuted = attr.IsMuted;
            dto.MemorialPhotoEffect = attr.MemorialPhotoEffect;
            dto.ContentIntervalSec = attr.ContentIntervalSec;
            
            if (!string.IsNullOrEmpty(attr.VideoId))
            {
                var video = await _context.MediaSources.FindAsync(attr.VideoId);
                dto.VideoName = video?.ShortName ?? video?.Name;
            }
            if (!string.IsNullOrEmpty(attr.MusicId))
            {
                var music = await _context.MediaSources.FindAsync(attr.MusicId);
                dto.MusicName = music?.ShortName ?? music?.Name;
            }
        }

        return dto;
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
            LastSeenAt = item.Status == "ONLINE" ? DateTime.UtcNow : null,
            SortOrder = item.SortOrder,
            BuildingId = item.BuildingId,
            FloorId = item.FloorId,
            RoomId = item.RoomId,
            CompanyId = item.CompanyId,
        };

        _context.Devices.Add(entity);
        await _context.SaveChangesAsync();

        // 실시간 변경 알림 송신
        try
        {
            await _deviceHubSender.SendDeviceChangedByDeviceIdAsync(entity.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR 장비 생성 알림 전송 중 에러 발생");
        }

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
        if (item.Status == "ONLINE")
        {
            entity.LastSeenAt = DateTime.UtcNow;
        }
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

            // 실시간 변경 알림 송신
            try
            {
                await _deviceHubSender.SendDeviceChangedByDeviceIdAsync(id);
                _logger.LogInformation("장비 수정 완료 SignalR 알림 송신. Code: {Code}", entity.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR 장비 수정 알림 전송 중 에러 발생");
            }

            _logger.LogInformation("장비 수정 완료. Code: {Code}", entity.Code);
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "장비 수정 중 오류가 발생했습니다. Code: {Code}", entity.Code);
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
            _logger.LogInformation("장비 삭제 완료. Code: {Code}", entity.Code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "장비 삭제 중 오류가 발생했습니다. Code: {Code}", entity.Code);
            throw;
        }
    }

    /// <summary>
    /// 장비 상태 및 마지막 확인 시간 업데이트
    /// </summary>
    public async Task<bool> UpdateStatusAsync(string deviceCode, string status, string? ipAddress = null, string? macAddress = null, string? publicIpAddress = null)
    {
        _logger.LogInformation("장비 상태를 업데이트합니다. Code: {Code}, Status: {Status}, IP: {IP}, MAC: {MAC}, PublicIP: {PublicIP}", deviceCode, status, ipAddress, macAddress, publicIpAddress);

        var entity = await _context.Devices.FirstOrDefaultAsync(d => d.Code == deviceCode && !d.IsDeleted);
        if (entity == null)
        {
            _logger.LogWarning("상태를 업데이트할 장비를 찾을 수 없습니다. Code: {Code}", deviceCode);
            return false;
        }

        entity.Status = status;
        entity.LastSeenAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(ipAddress))
        {
            entity.IpAddress = ipAddress;
        }
        if (!string.IsNullOrEmpty(macAddress))
        {
            entity.MacAddress = macAddress;
        }
        if (!string.IsNullOrEmpty(publicIpAddress) && publicIpAddress != "::1" && publicIpAddress != "127.0.0.1")
        {
            entity.PublicIpAddress = publicIpAddress;
        }

        try
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // 동일한 물리 기기(IP 또는 MAC)를 공유하는 다른 기기코드가 있다면 즉시 OFFLINE 전환
            if (status == "ONLINE")
            {
                var otherDevicesQuery = _context.Devices.Where(d => d.Id != entity.Id && d.Status == "ONLINE" && !d.IsDeleted);
                List<Device> otherDevices = new();

                if (!string.IsNullOrEmpty(entity.MacAddress))
                {
                    otherDevices = await otherDevicesQuery.Where(d => d.MacAddress == entity.MacAddress).ToListAsync();
                }
                else if (!string.IsNullOrEmpty(entity.IpAddress))
                {
                    otherDevices = await otherDevicesQuery.Where(d => d.IpAddress == entity.IpAddress).ToListAsync();
                }

                foreach (var other in otherDevices)
                {
                    other.Status = "OFFLINE";
                    other.UpdatedAt = DateTime.UtcNow;
                    _context.Entry(other).State = EntityState.Modified;

                    _logger.LogInformation("동일 기기(IP/MAC) 감지로 기존 장비 즉시 오프라인 처리. OldCode: {OtherCode}", other.Code);

                    try
                    {
                        await _deviceHubSender.SendDeviceStatusChangedAsync(other.Code, "OFFLINE");
                        await _deviceHubSender.SendDeviceChangedByDeviceIdAsync(other.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "이전 장비 상태 변경 알림 전송 실패. Code: {OtherCode}", other.Code);
                    }
                }

                if (otherDevices.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
            
            // 실시간 변경 알림 송신
            try
            {
                await _deviceHubSender.SendDeviceStatusChangedAsync(entity.Code, status);
                await _deviceHubSender.SendDeviceChangedByDeviceIdAsync(entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR 장비 상태 변경 알림 전송 중 에러 발생");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "장비 상태 업데이트 중 오류 발생. Code: {Code}", deviceCode);
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
        PublicIpAddress = d.PublicIpAddress,
        Status = d.Status,
        LastSeenAt = d.LastSeenAt,
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
