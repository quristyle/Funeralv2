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

    /// <summary>
    /// 보낸 <b>열쇠만</b> 갈아 끼운다. 나머지 칸은 저장된 것 그대로 남는다.
    /// </summary>
    /// <param name="userIdOrKey">로그인 아이디 또는 계정 키</param>
    /// <param name="patch">고칠 열쇠만 담은 객체. 값이 <c>null</c> 인 열쇠는 <b>지운다</b>.</param>
    /// <remarks>
    /// <para>
    /// <b><see cref="SaveAsync"/> 로는 안 되는 일이 있다.</b> 저것은 보낸 것을
    /// 통째로 덮어쓰므로, 설정 하나를 고치려면 프론트가 <b>전부</b>를 들고
    /// 있다가 함께 보내야 한다. 그런데 이 값은 이제 <b>기기를 넘나든다</b> —
    /// 휴대폰에서 고친 것이 책상 화면에 그대로 떠야 하는 것이 요구다. 그러면
    /// 책상 화면이 들고 있는 것은 <b>로그인하던 때의 한 벌</b>이라, 거기서
    /// 토스트 위치를 하나 고치는 순간 휴대폰이 방금 바꾼 띠가 <b>옛 것으로
    /// 되돌아간다.</b>
    /// </para>
    /// <para>
    /// 한 칸만 보내게 하면 그 사고가 없어진다. 겹치는 것은 <b>같은 열쇠를
    /// 동시에 고칠 때</b>뿐이고, 그때는 나중 것이 이기는 것이 옳다.
    /// </para>
    /// <para>
    /// <b>한 겹만 섞는다.</b> 깊이 들어가며 섞으면 배열을 어떻게 다룰지가
    /// 매번 달라진다 — 띠에 놓을 칸들(배열)은 통째로 갈아 끼우는 것이 맞는데,
    /// 깊은 병합은 그것을 자리별로 섞으려 든다.
    /// </para>
    /// </remarks>
    Task<SavePreferenceResult> MergeAsync(string userIdOrKey, JsonElement patch);
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
