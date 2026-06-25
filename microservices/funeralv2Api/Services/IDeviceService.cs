using funeralv2Api.DTOs;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Services;

public interface IDeviceService
{
    Task<ApiResult<DeviceDto>> GetByIdAsync(string id);
    Task<ApiResult<IReadOnlyList<DeviceDto>>> GetAllAsync();
    Task<ApiResult<string>> CreateAsync(DeviceCreateDto item);
    Task<ApiResult<bool>> UpdateAsync(string id, DeviceUpdateDto item);
    Task<ApiResult<bool>> DeleteAsync(string id);
    Task<ApiResult<IReadOnlyList<DeviceDto>>> GetByFilterAsync(string? companyId, string? buildingId, string? floorId, string? roomId);
}
