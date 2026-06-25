using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Funeralv2.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(AppDbContext context, ILogger<DeviceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<IReadOnlyList<DeviceDto>>> GetAllAsync()
    {
        var devices = await _context.Devices
            .AsNoTracking()
            .ToListAsync();
        
        var dtos = devices.Select(d => new DeviceDto { 
            Id = d.Id,
            Name = d.Name,
            Code = d.Code,
            DeviceType = d.DeviceType,
            IpAddress = d.IpAddress,
            MacAddress = d.MacAddress,
            Status = d.Status,
            BuildingId = d.BuildingId,
            FloorId = d.FloorId,
            RoomId = d.RoomId,
        }).ToList();

        return ApiResult<IReadOnlyList<DeviceDto>>.Success(dtos);
    }
    
    public async Task<ApiResult<IReadOnlyList<DeviceDto>>> GetByFilterAsync(string? companyId, string? buildingId, string? floorId, string? roomId)
    {
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
            // Join with Buildings to filter by companyId
            query = from device in query
                    join building in _context.Buildings on device.BuildingId equals building.Id
                    where building.CompanyId == companyId
                    select device;
        } else {
             return ApiResult<IReadOnlyList<DeviceDto>>.Success(new List<DeviceDto>());
        }

        var devices = await query.ToListAsync();
        
        var dtos = devices.Select(d => new DeviceDto { 
            Id = d.Id,
            Name = d.Name,
            Code = d.Code,
            DeviceType = d.DeviceType,
            IpAddress = d.IpAddress,
            MacAddress = d.MacAddress,
            Status = d.Status,
            BuildingId = d.BuildingId,
            FloorId = d.FloorId,
            RoomId = d.RoomId,
        }).ToList();
        
        return ApiResult<IReadOnlyList<DeviceDto>>.Success(dtos);
    }


    public async Task<ApiResult<DeviceDto>> GetByIdAsync(string id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return ApiResult<DeviceDto>.Failure("Device not found.");
        }
        
        var dto = new DeviceDto {
             Id = device.Id,
            Name = device.Name,
            Code = device.Code,
            DeviceType = device.DeviceType,
            IpAddress = device.IpAddress,
            MacAddress = device.MacAddress,
            Status = device.Status,
            BuildingId = device.BuildingId,
            FloorId = device.FloorId,
            RoomId = device.RoomId,
        };

        return ApiResult<DeviceDto>.Success(dto);
    }

    public async Task<ApiResult<string>> CreateAsync(DeviceCreateDto item)
    {
        try
        {
            var entity = new Device
            {
                Name = item.Name,
                Code = item.Code,
                DeviceType = item.DeviceType,
                IpAddress = item.IpAddress,
                MacAddress = item.MacAddress,
                Status = item.Status,
                BuildingId = item.BuildingId,
                FloorId = item.FloorId,
                RoomId = item.RoomId,
            };

            _context.Devices.Add(entity);
            await _context.SaveChangesAsync();

            return ApiResult<string>.Success(entity.Id, "Device created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating device.");
            return ApiResult<string>.Failure("Error creating device.");
        }
    }

    public async Task<ApiResult<bool>> UpdateAsync(string id, DeviceUpdateDto item)
    {
        var entity = await _context.Devices.FindAsync(id);
        if (entity == null)
        {
            return ApiResult<bool>.Failure("Device not found.");
        }

        entity.Name = item.Name;
        entity.DeviceType = item.DeviceType;
        entity.IpAddress = item.IpAddress;
        entity.MacAddress = item.MacAddress;
        entity.Status = item.Status;
        entity.BuildingId = item.BuildingId;
        entity.FloorId = item.FloorId;
        entity.RoomId = item.RoomId;

        try
        {
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return ApiResult<bool>.Success(true, "Device updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device.");
            return ApiResult<bool>.Failure("Error updating device.");
        }
    }

    public async Task<ApiResult<bool>> DeleteAsync(string id)
    {
        var entity = await _context.Devices.FindAsync(id);
        if (entity == null)
        {
            return ApiResult<bool>.Failure("Device not found.");
        }

        try
        {
            _context.Devices.Remove(entity);
            await _context.SaveChangesAsync();
            return ApiResult<bool>.Success(true, "Device deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device.");
            return ApiResult<bool>.Failure("Error deleting device.");
        }
    }
}
