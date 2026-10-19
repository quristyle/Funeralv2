using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 빈소 현황 서비스. 현황 화면 다섯이 모두 이것을 쓴다.
/// </summary>
public interface IStatusService
{
    /// <summary>현황 목록과 요약을 함께 돌려준다.</summary>
    Task<FuneralStatusBoardDto> GetBoardAsync(string? buildingId, string? floorId, bool onlyInUse);

    /// <summary>빈소 한 칸의 현황</summary>
    Task<FuneralStatusDto?> GetRoomStatusAsync(string roomId);

    /// <summary>
    /// 빈소현황 대시보드 — 호실·고인·장비를 서버에서 붙여 한 번에 준다.
    /// </summary>
    Task<RoomBoardDto> GetRoomBoardAsync(RoomBoardQueryDto query);
}
