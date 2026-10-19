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
    /// 그 계정의 비밀번호를 <b>새 값으로 발급해 갈아 끼운다</b>(관리자용).
    /// 계정을 못 찾으면 <c>null</c>.
    /// </summary>
    /// 지금 비밀번호를 묻지 않는다 — 잊은 사람을 대신 풀어 주는 길이라 물을 수 없다. 그래서 부르는 쪽이 관리자인지 먼저 본다.
    Task<IssuedPasswordDto?> ResetAccountPasswordAsync(string id);

    /// <summary>
    /// 로그인한 사용자의 프로필 정보를 업데이트합니다.
    /// </summary>
    Task<(bool Success, string? Error)> UpdateProfileAsync(string userId, UpdateProfileDto dto);

    /// <summary>
    /// 로그인한 사용자의 비밀번호를 변경합니다.
    /// </summary>
    /// 90일 만료 때문에 사용자가 어쩔 수 없이 오므로, 실패 이유를 구분해서 돌려준다.
    Task<ChangePasswordResult> ChangePasswordAsync(string userId, ChangePasswordDto dto);

    /// <summary>
    /// 비밀번호가 맞는지 <b>확인만</b> 한다. 아무것도 바꾸지 않는다.
    /// </summary>
    /// 잠금화면이 쓴다. 로그인 API 를 다시 부르면 새 토큰이 기존 세션과 섞이므로 확인만 하는 길을 따로 둔다.
    /// 「계정 없음」과 「비밀번호 틀림」을 구분하지 않는다 — 구분하면 계정을 골라내는 데 쓰인다.
    Task<bool> VerifyPasswordAsync(string userId, string password);

    /// <summary>
    /// 로그인한 사용자의 설정을 업데이트합니다.
    /// </summary>
    Task<bool> UpdateSettingAsync(string userId, UpdateSettingDto dto);

    /// <summary>
    /// 로그인한 뒤 처음 열리는 화면을 정한다.
    /// </summary>
    /// 담기는 값이 켬·끔뿐이라 UpdateSettingAsync 와 따로 둔다 — 글자를 실을 수 있게 되면 어느 칸이든 임의의 글자를 적는 길이 열린다.
    /// <param name="userId">로그인 아이디 또는 계정 식별자.</param>
    /// <param name="homePath">
    /// 메뉴 경로. 비었으면 지정을 <b>푼다</b>(줄을 지운다).
    /// </param>
    /// <returns>
    /// 저장했으면 <c>(true, null)</c>. 경로가 이 사이트 밖을 가리키거나 계정을
    /// 못 찾으면 <c>(false, 까닭)</c>.
    /// </returns>
    Task<(bool Success, string? Error)> UpdateHomePathAsync(string userId, string? homePath);

    /// <summary>
    /// 기존 계정을 대상 계정으로 결합(Merge)합니다.
    /// 소셜 연동, 연락처, WebAuthn 자격증명 등이 대상 계정으로 이전되고 원본 계정은 삭제됩니다.
    /// </summary>
    /// <param name="sourceAccountId">결합될 원본 계정의 식별자(Id 또는 UserId)</param>
    /// <param name="targetAccountId">결합을 받아들일 대상 계정의 식별자(Id 또는 UserId)</param>
    /// <param name="approver">작업 수행자(관리자 아이디)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>성공 여부 및 실패 시 오류 메시지</returns>
    Task<(bool Ok, string? Error)> MergeAccountAsync(
        string sourceAccountId, string targetAccountId, string approver, CancellationToken ct = default);
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
