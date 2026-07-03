using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 리본 설정 서비스 인터페이스
/// </summary>
public interface IDeviceRibbonService
{
    /// <summary>장비 ID로 리본 목록 조회</summary>
    Task<List<DeviceRibbonDto>> GetByDeviceIdAsync(string deviceId);

    /// <summary>리본 단건 조회</summary>
    Task<DeviceRibbonDto?> GetByIdAsync(string id);

    /// <summary>리본 단건 생성</summary>
    Task<DeviceRibbonDto> CreateAsync(DeviceRibbonUpsertDto dto);

    /// <summary>리본 단건 수정</summary>
    Task<DeviceRibbonDto?> UpdateAsync(string id, DeviceRibbonUpsertDto dto);

    /// <summary>리본 단건 삭제</summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>장비의 전체 리본 목록 일괄 저장 (기존 삭제 후 재삽입)</summary>
    Task<List<DeviceRibbonDto>> BulkSaveAsync(DeviceRibbonBulkSaveDto dto);
}
