using System.Text.Json;

namespace JSini.Web.Components.Security;

/// <summary>
/// 잠금화면이 <c>passkey.js</c> · 게이트웨이와 주고받는 값들.
///
/// <para>
/// <b>「내 정보 → 보안 설정」의 같은 이름들과 일부러 겹쳐 두지 않았다.</b>
/// 그쪽은 포털관리 모듈(<c>JSini.Web.Admin</c>)에 있고, 잠금화면은 레이아웃에
/// 얹히는 공용 부품이라 <b>업무 모듈을 참조할 수 없다</b>(web/CLAUDE.md
/// 의존 규칙 4번). 셋째 손님이 생기면 그때 <c>JSini.Web.Models</c> 로
/// 올린다 — 「둘이면 복제, 셋부터 승격」.
/// </para>
/// </summary>
internal static class LockPasskey
{
    /// <summary>도전값을 받는 자리. 게이트웨이 경로다(셸의 <c>/passkey/options</c> 가 아니다).</summary>
    /// <remarks>
    /// 로그인 화면이 셸을 거치는 것은 <b>회로가 없어서</b>다. 잠금화면은 회로
    /// 안에 있어 C# 이 게이트웨이를 직접 부를 수 있고, 그래야 토큰이 실린다 —
    /// 이 경로는 익명이 아니라 <b>지금 로그인한 사람</b>의 기기 목록을 준다.
    /// </remarks>
    public const string OptionsPath = "auth/webauthn/verify/options";

    /// <summary>받은 서명을 확인받는 자리. 참·거짓만 돌아오고 토큰은 바뀌지 않는다.</summary>
    public const string VerifyPath = "auth/webauthn/verify";
}

/// <summary>
/// 서버가 낸 도전값 한 벌. <b>브라우저에 그대로 넘긴다</b> — 그 안을 C# 이
/// 들여다볼 이유가 없다.
/// </summary>
internal sealed class LockPasskeyOptionsDto
{
    /// <summary>이 도전값을 다시 찾을 번호. 확인 요청에 그대로 실어 보낸다.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// <c>navigator.credentials.get()</c> 에 넘길 설정. 이진값은 base64url
    /// 문자열이고 <c>passkey.js</c> 가 바이트로 되돌린다.
    /// </summary>
    public JsonElement PublicKey { get; set; }
}

/// <summary>
/// 기기가 내준 서명. <c>passkey.js</c> 의 <c>verify</c> 가 돌려주는 모양 그대로다.
/// </summary>
/// <remarks>
/// <see cref="Ok"/> 가 거짓이면 <see cref="Error"/> 에 <b>사용자에게 그대로
/// 보여 줄 문구</b>가 들어 있다 — 브라우저가 던진 원문이 아니라 그쪽에서
/// 이미 사람 말로 옮긴 것이다.
/// </remarks>
internal sealed class LockPasskeyAssertionDto
{
    public bool Ok { get; set; }

    public string? Error { get; set; }

    /// <summary>
    /// 이 계정에 등록된 기기가 <b>하나도 없어서</b> 기기를 부르지도 않았는가.
    /// 참이면 단추를 감춘다 — 남겨 두면 누를 때마다 같은 말만 나온다.
    /// </summary>
    public bool Empty { get; set; }

    public string SessionId { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string ClientDataJson { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? UserHandle { get; set; }
}

/// <summary>이 브라우저가 패스키를 다룰 수 있는가. <c>passkey.js</c> 의 <c>status</c>.</summary>
internal sealed class LockPasskeyBrowserDto
{
    /// <summary>
    /// WebAuthn 을 쓸 수 있는가. <b>http 로 열면 거짓이다</b> —
    /// 보안 문맥이 아니면 API 자체가 없기 때문이다.
    /// </summary>
    public bool Supported { get; set; }

    /// <summary>이 기기에 붙박이 인증기(지문·얼굴·Hello)가 있는가.</summary>
    public bool PlatformAuthenticator { get; set; }

    /// <summary>
    /// 이 기기가 <b>로그인할 때</b> 무엇을 먼저 묻는가(<c>password</c> ·
    /// <c>passkey</c>). 잠금화면은 이 값으로 <b>차례만</b> 바꾼다 —
    /// 기기 확인을 스스로 열지는 않는다. 까닭은 <c>LockScreen</c> 머리말에.
    /// </summary>
    public string Priority { get; set; } = "password";
}
