using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 장비 관리 서비스 인터페이스
/// </summary>
public interface IDeviceService
{
    /// <summary>장비 전체 목록 조회</summary>
    Task<List<DeviceDto>> GetAllAsync();

    /// <summary>필터 기반 장비 목록 조회 (회사, 건물, 층, 호실)</summary>
    Task<List<DeviceDto>> GetByFilterAsync(string? companyId, string? buildingId, string? floorId, string? roomId);

    /// <summary>단일 장비 상세 조회</summary>
    Task<DeviceDto?> GetByIdAsync(string id);

    /// <summary>장비 생성</summary>
    Task<string> CreateAsync(DeviceCreateDto item);

    /// <summary>장비 수정</summary>
    Task<bool> UpdateAsync(string id, DeviceUpdateDto item);

    /// <summary>장비 삭제</summary>
    Task<bool> DeleteAsync(string id);
}
