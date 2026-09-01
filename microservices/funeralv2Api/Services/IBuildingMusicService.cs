using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 건물별 음원 배정 서비스 (옛 <c>t_music_build</c>).
/// </summary>
public interface IBuildingMusicService
{
    /// <summary>
    /// 건물 목록에 이 음원의 배정 여부를 붙여 돌려준다.
    /// 배정되지 않은 건물도 함께 나온다 — 화면이 체크박스로 켜고 끄기 때문이다.
    /// </summary>
    Task<List<BuildingMusicDto>> GetBuildingsForMusicAsync(string mediaSourceId);

    /// <summary>음원 하나의 배정을 통째로 바꾼다.</summary>
    Task<List<BuildingMusicDto>> SaveAsync(string userId, string mediaSourceId, List<string> buildingIds);

    /// <summary>한 건물에 배정된 음원 아이디 목록 (장비가 재생 목록을 받아 갈 때 쓴다).</summary>
    Task<List<string>> GetMusicIdsForBuildingAsync(string buildingId);
}
