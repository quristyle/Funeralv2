using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 기본 설정 관리 서비스 구현체
/// </summary>
public class DeviceConfigService : IDeviceConfigService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceConfigService> _logger;

    public DeviceConfigService(AppDbContext context, ILogger<DeviceConfigService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<DeviceConfigDto>> GetListByDeviceIdAsync(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return [];
        }

        var config = await _context.DeviceConfigs
            .AsNoTracking()
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        return config == null ? [] : [MapToDto(config)];
    }

    /// <inheritdoc />
    public async Task<DeviceConfigDto?> GetByDeviceIdAsync(string deviceId)
    {
        _logger.LogInformation("장비 기본 설정 조회. DeviceId: {DeviceId}", deviceId);

        var config = await _context.DeviceConfigs
            .AsNoTracking()
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        return config == null ? null : MapToDto(config);
    }

    /// <inheritdoc />
    public async Task<DeviceConfigDto> UpsertAsync(DeviceConfigUpsertDto dto)
    {
        _logger.LogInformation("장비 기본 설정 Upsert. DeviceId: {DeviceId}", dto.DeviceId);

        var existing = await _context.DeviceConfigs
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.DeviceId == dto.DeviceId);

        if (existing == null)
        {
            existing = new DeviceConfig { DeviceId = dto.DeviceId };
            _context.DeviceConfigs.Add(existing);
            _logger.LogInformation("장비 기본 설정 신규 생성. DeviceId: {DeviceId}", dto.DeviceId);
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("장비 기본 설정 수정. DeviceId: {DeviceId}, Id: {Id}", dto.DeviceId, existing.Id);
        }

        ApplyDto(existing, dto);
        await _context.SaveChangesAsync();

        if (existing.Device == null)
        {
            existing.Device = await _context.Devices.FindAsync(dto.DeviceId);
        }

        return MapToDto(existing);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(string id, DeviceConfigUpsertDto dto)
    {
        _logger.LogInformation("장비 기본 설정 수정. Id: {Id}", id);

        var existing = await _context.DeviceConfigs
            .Include(c => c.Device)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (existing == null)
        {
            _logger.LogWarning("수정할 장비 기본 설정을 찾을 수 없습니다. Id: {Id}", id);
            return false;
        }

        existing.DeviceId = dto.DeviceId;
        existing.UpdatedAt = DateTime.UtcNow;
        ApplyDto(existing, dto);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteByDeviceIdAsync(string deviceId)
    {
        _logger.LogInformation("장비 기본 설정 삭제. DeviceId: {DeviceId}", deviceId);

        var config = await _context.DeviceConfigs
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (config == null)
        {
            _logger.LogWarning("삭제할 장비 기본 설정이 없습니다. DeviceId: {DeviceId}", deviceId);
            return false;
        }

        _context.DeviceConfigs.Remove(config);
        await _context.SaveChangesAsync();
        return true;
    }

    private static void ApplyDto(DeviceConfig entity, DeviceConfigUpsertDto dto)
    {
        entity.Volume = dto.Volume;
        entity.Brightness = dto.Brightness;
        entity.RebootTime = NormalizeTime(dto.RebootTime);
        entity.IsAutoPower = dto.IsAutoPower;
        entity.PowerOnTime = NormalizeTime(dto.PowerOnTime);
        entity.PowerOffTime = NormalizeTime(dto.PowerOffTime);
    }

    private static string? NormalizeTime(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DeviceConfigDto MapToDto(DeviceConfig c) => new()
    {
        Id = c.Id,
        DeviceId = c.DeviceId,
        DeviceName = c.Device?.Name,
        Volume = c.Volume,
        Brightness = c.Brightness,
        RebootTime = c.RebootTime,
        IsAutoPower = c.IsAutoPower,
        PowerOnTime = c.PowerOnTime,
        PowerOffTime = c.PowerOffTime,
    };
}
