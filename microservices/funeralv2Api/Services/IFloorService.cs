using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 층 관리 서비스 인터페이스
/// </summary>
public interface IFloorService
{
    /// <summary>
    /// 층 목록 조회 (건물 필터 적용)
    /// </summary>
    Task<List<FloorDto>> GetFloorsAsync(string? buildingId);

    /// <summary>
    /// 단일 층 상세 조회
    /// </summary>
    Task<FloorDto?> GetFloorByIdAsync(string id);

    /// <summary>
    /// 층 생성
    /// </summary>
    Task<FloorDto> CreateFloorAsync(FloorCreateDto dto);

    /// <summary>
    /// 층 수정
    /// </summary>
    Task<FloorDto?> UpdateFloorAsync(string id, FloorUpdateDto dto);

    /// <summary>
    /// 층 삭제
    /// </summary>
    Task<bool> DeleteFloorAsync(string id);
}
