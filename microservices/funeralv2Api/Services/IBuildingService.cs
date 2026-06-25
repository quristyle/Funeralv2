using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 건물 관리 서비스 인터페이스
/// </summary>
public interface IBuildingService
{
    /// <summary>
    /// 건물 목록 조회 (회사 필터 적용)
    /// </summary>
    Task<List<BuildingDto>> GetBuildingsAsync(string? companyId);

    /// <summary>
    /// 단일 건물 상세 조회
    /// </summary>
    Task<BuildingDto?> GetBuildingByIdAsync(string id);

    /// <summary>
    /// 건물 생성
    /// </summary>
    Task<BuildingDto> CreateBuildingAsync(BuildingCreateDto dto);

    /// <summary>
    /// 건물 수정
    /// </summary>
    Task<BuildingDto?> UpdateBuildingAsync(string id, BuildingUpdateDto dto);

    /// <summary>
    /// 건물 삭제
    /// </summary>
    Task<bool> DeleteBuildingAsync(string id);
}
