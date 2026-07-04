using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 텍스트 오버레이 서비스 인터페이스
/// </summary>
public interface IDeviceTextOverlayService
{
    Task<List<DeviceTextOverlayDto>> GetByDeviceIdAsync(string deviceId);
    Task<DeviceTextOverlayDto?> GetByIdAsync(string id);
    Task<DeviceTextOverlayDto> CreateAsync(DeviceTextOverlayUpsertDto dto);
    Task<DeviceTextOverlayDto?> UpdateAsync(string id, DeviceTextOverlayUpsertDto dto);
    Task<bool> DeleteAsync(string id);
    Task<List<DeviceTextOverlayDto>> BulkSaveAsync(DeviceTextOverlayBulkSaveDto dto);
}
