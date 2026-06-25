using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 호실 관리 서비스 인터페이스
/// </summary>
public interface IRoomService
{
    /// <summary>
    /// 호실 목록 조회 (층 필터 적용)
    /// </summary>
    Task<List<RoomDto>> GetRoomsAsync(string? floorId);

    /// <summary>
    /// 단일 호실 상세 조회
    /// </summary>
    Task<RoomDto?> GetRoomByIdAsync(string id);

    /// <summary>
    /// 호실 생성
    /// </summary>
    Task<RoomDto> CreateRoomAsync(RoomCreateDto dto);

    /// <summary>
    /// 호실 수정
    /// </summary>
    Task<RoomDto?> UpdateRoomAsync(string id, RoomUpdateDto dto);

    /// <summary>
    /// 호실 삭제
    /// </summary>
    Task<bool> DeleteRoomAsync(string id);
}
