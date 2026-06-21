using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 사용자 프로필 및 상세 정보를 관리하는 서비스 인터페이스
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 특정 사용자의 상세 정보를 조회합니다.
    /// </summary>
    /// <param name="userIdOrKey">사용자 아이디 또는 고유 키</param>
    /// <returns>사용자 정보 DTO, 찾을 수 없는 경우 null</returns>
    Task<UserInfoDto?> GetUserInfoAsync(string userIdOrKey);

    /// <summary>
    /// 전체 계정 목록을 조회합니다.
    /// </summary>
    Task<List<AccountDto>> GetAccountsAsync();

    /// <summary>
    /// 신규 계정을 생성합니다.
    /// </summary>
    Task<AccountDto> CreateAccountAsync(CreateAccountDto dto);

    /// <summary>
    /// 기존 계정 정보를 수정합니다.
    /// </summary>
    Task<bool> UpdateAccountAsync(string id, UpdateAccountDto dto);

    /// <summary>
    /// 계정을 삭제합니다.
    /// </summary>
    Task<bool> DeleteAccountAsync(string id);
}
