using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>릴리스 요청의 결말</summary>
public enum PlayerReleaseOutcome
{
    Ok,
    /// <summary>릴리스 권한이 없다</summary>
    Forbidden,
    /// <summary>버전 형식이 틀렸거나 이미 있는 태그다</summary>
    Invalid,
    /// <summary>서버에 GitHub 설정이 없다</summary>
    NotConfigured,
    /// <summary>GitHub 이 거절했거나 응답하지 않는다</summary>
    Failed,
}

/// <summary>
/// 플레이어(funeralv2_player) 릴리스를 GitHub 에 요청하는 서비스.
/// </summary>
public interface IPlayerReleaseService
{
    /// <summary>화면을 처음 그릴 때 필요한 것(설정 여부·권한·최신 커밋·기존 태그).</summary>
    Task<PlayerReleaseStatusDto> GetStatusAsync(string userId);

    /// <summary>
    /// 버전 태그를 만들어 릴리스 워크플로를 깨운다.
    /// </summary>
    Task<(PlayerReleaseOutcome Outcome, PlayerReleaseResultDto Result)> CreateAsync(
        string userId, PlayerReleaseRequestDto request);

    /// <summary>그 태그로 도는 워크플로의 진행 상황. 화면이 폴링한다.</summary>
    Task<PlayerReleaseRunDto> GetRunAsync(string tag);
}
