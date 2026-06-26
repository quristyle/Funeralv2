using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 기본 설정 관리 서비스 인터페이스
/// </summary>
public interface IDeviceConfigService
{
    /// <summary>장비 ID로 기본 설정 목록 조회 (0~1건)</summary>
    Task<List<DeviceConfigDto>> GetListByDeviceIdAsync(string? deviceId);

    /// <summary>장비 ID로 기본 설정 조회</summary>
    Task<DeviceConfigDto?> GetByDeviceIdAsync(string deviceId);

    /// <summary>장비 기본 설정 Upsert (없으면 생성, 있으면 수정)</summary>
    Task<DeviceConfigDto> UpsertAsync(DeviceConfigUpsertDto dto);

    /// <summary>장비 기본 설정 수정</summary>
    Task<bool> UpdateAsync(string id, DeviceConfigUpsertDto dto);

    /// <summary>장비 기본 설정 삭제</summary>
    Task<bool> DeleteByDeviceIdAsync(string deviceId);
}
