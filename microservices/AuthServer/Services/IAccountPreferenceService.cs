using System.Text.Json;

namespace AuthServer.Services;

/// <summary>
/// 계정별 화면 환경설정 저장·조회.
/// </summary>
public interface IAccountPreferenceService
{
    /// <summary>
    /// 저장된 환경설정을 가져온다. 없으면 빈 객체(<c>{}</c>)를 준다.
    /// </summary>
    /// <param name="userIdOrKey">로그인 아이디 또는 계정 키</param>
    Task<JsonElement> GetAsync(string userIdOrKey);

    /// <summary>
    /// 환경설정을 저장한다. 계정 하나에 한 행이라 있으면 덮어쓴다.
    /// </summary>
    Task<SavePreferenceResult> SaveAsync(string userIdOrKey, JsonElement payload);
}

/// <summary>환경설정 저장 결과.</summary>
public enum SavePreferenceResult
{
    /// <summary>저장했다.</summary>
    Success,

    /// <summary>계정을 찾지 못했다.</summary>
    AccountNotFound,

    /// <summary>
    /// 값이 너무 크다.
    /// 이 값은 사용자가 보낸 것을 그대로 보관하므로 상한이 없으면 DB 를 밀어 넣을 수 있다.
    /// </summary>
    TooLarge
}
