using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 빈소 현황 서비스. 현황 화면 다섯이 모두 이것을 쓴다.
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// 현황 목록과 요약을 함께 돌려준다.
    /// </summary>
    /// <param name="buildingId">건물로 좁힌다. 비우면 전부.</param>
    /// <param name="floorId">층으로 좁힌다. 비우면 전부.</param>
    /// <param name="onlyInUse">사용 중인 빈소만 볼지</param>
    Task<FuneralStatusBoardDto> GetBoardAsync(string? buildingId, string? floorId, bool onlyInUse);

    /// <summary>빈소 한 칸의 현황</summary>
    Task<FuneralStatusDto?> GetRoomStatusAsync(string roomId);

    /// <summary>
    /// 빈소현황 대시보드(<c>/room_status</c>) — 호실·고인·장비를 서버에서 붙여
    /// 한 번에 준다. 예전에는 화면이 네 목록을 받아 브라우저에서 조인했다.
    /// </summary>
    Task<RoomBoardDto> GetRoomBoardAsync(RoomBoardQueryDto query);
}
