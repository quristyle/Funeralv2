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

    /// <summary>
    /// 로그인한 사용자의 프로필 정보를 업데이트합니다.
    /// </summary>
    Task<(bool Success, string? Error)> UpdateProfileAsync(string userId, UpdateProfileDto dto);

    /// <summary>
    /// 로그인한 사용자의 비밀번호를 변경합니다.
    /// </summary>
    /// <remarks>
    /// 90일 만료 정책 때문에 사용자가 <b>어쩔 수 없이</b> 이 화면에 오는 경우가 생긴다.
    /// 그때 "변경에 실패했습니다" 한 마디만 돌려주면 무엇을 고쳐야 하는지 알 수 없으므로,
    /// 실패 이유를 구분해서 돌려준다.
    /// </remarks>
    Task<ChangePasswordResult> ChangePasswordAsync(string userId, ChangePasswordDto dto);

    /// <summary>
    /// 비밀번호가 맞는지 <b>확인만</b> 한다. 아무것도 바꾸지 않는다.
    /// </summary>
    /// <remarks>
    /// 잠금화면이 쓴다(D7). 로그인 API 를 다시 부르는 방법도 있었지만 그러면
    /// <b>새 토큰이 발급되어 기존 세션과 섞인다</b> — 잠금을 푸는 일이 조용히
    /// 재로그인이 되는 셈이라, 확인만 하는 길을 따로 뒀다.
    ///
    /// <para>
    /// 돌려주는 것은 참·거짓뿐이다. "계정이 없다" 와 "비밀번호가 틀리다" 를
    /// 구분해 주지 않는다 — 여기 오는 사람은 이미 로그인한 사람이라
    /// 구분해 봐야 알려 줄 것이 없고, 구분하면 계정을 골라내는 데 쓰인다.
    /// </para>
    /// </remarks>
    Task<bool> VerifyPasswordAsync(string userId, string password);

    /// <summary>
    /// 로그인한 사용자의 설정을 업데이트합니다.
    /// </summary>
    Task<bool> UpdateSettingAsync(string userId, UpdateSettingDto dto);

    /// <summary>
    /// 로그인한 뒤 처음 열리는 화면을 정한다.
    /// </summary>
    /// <remarks>
    /// <see cref="UpdateSettingAsync"/> 와 담기는 표가 같은데도 따로 두는 까닭은
    /// <b>담기는 값의 꼴이 다르기</b> 때문이다 — 저쪽은 켬·끔(<c>bool</c>)만
    /// 받아 <c>"true"</c>/<c>"false"</c> 로 적는다. 여기에 글자를 실으려면
    /// 저 DTO 에 칸을 하나 더해야 하고, 그러면 <b>어느 칸이든 임의의 글자를
    /// 임의의 <c>DetailType</c> 에 적을 수 있는 길</b>이 열린다.
    /// </remarks>
    /// <param name="userId">로그인 아이디 또는 계정 식별자.</param>
    /// <param name="homePath">
    /// 메뉴 경로. 비었으면 지정을 <b>푼다</b>(줄을 지운다).
    /// </param>
    /// <returns>
    /// 저장했으면 <c>(true, null)</c>. 경로가 이 사이트 밖을 가리키거나 계정을
    /// 못 찾으면 <c>(false, 까닭)</c>.
    /// </returns>
    Task<(bool Success, string? Error)> UpdateHomePathAsync(string userId, string? homePath);
}

/// <summary>
/// 비밀번호 변경 결과.
/// </summary>
public enum ChangePasswordResult
{
    /// <summary>변경했다.</summary>
    Success,

    /// <summary>계정을 찾지 못했다.</summary>
    AccountNotFound,

    /// <summary>이전 비밀번호가 맞지 않다.</summary>
    OldPasswordMismatch,

    /// <summary>
    /// 새 비밀번호가 비어 있다.
    /// </summary>
    NewPasswordEmpty,

    /// <summary>
    /// 새 비밀번호가 지금 쓰는 것과 같다.
    /// 90일마다 바꾸라고 하면서 같은 값을 허용하면 정책이 아무 일도 하지 않는다.
    /// </summary>
    SameAsCurrent
}
