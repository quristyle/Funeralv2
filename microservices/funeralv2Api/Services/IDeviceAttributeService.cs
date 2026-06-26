using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 속성 관리 서비스 인터페이스
/// </summary>
public interface IDeviceAttributeService
{
    /// <summary>장비 ID로 속성 조회</summary>
    Task<DeviceAttributeDto?> GetByDeviceIdAsync(string deviceId);

    /// <summary>장비 속성 Upsert (없으면 생성, 있으면 수정)</summary>
    Task<DeviceAttributeDto> UpsertAsync(DeviceAttributeUpsertDto dto);

    /// <summary>장비 속성 삭제</summary>
    Task<bool> DeleteByDeviceIdAsync(string deviceId);
}
