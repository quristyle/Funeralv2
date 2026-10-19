using System.Text.Json;

namespace JSini.Web.Components.Security;

/// <summary>잠금화면이 passkey.js · 게이트웨이와 주고받는 값들. 「내 정보 → 보안 설정」과 이름을 일부러 겹치지 않는다(잠금화면은 공용 부품이라 업무 모듈을 참조할 수 없다).</summary>
internal static class LockPasskey
{
    /// <summary>도전값을 받는 자리(게이트웨이 경로). 회로 안에서 부르므로 토큰이 실리고, 지금 로그인한 사람의 기기 목록을 받는다.</summary>
    public const string OptionsPath = "auth/webauthn/verify/options";

    /// <summary>받은 서명을 확인받는 자리. 참·거짓만 돌아오고 토큰은 바뀌지 않는다.</summary>
    public const string VerifyPath = "auth/webauthn/verify";
}

/// <summary>서버가 낸 도전값 한 벌. 브라우저에 그대로 넘긴다.</summary>
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

/// <summary>기기가 내준 서명. passkey.js 의 verify 가 돌려주는 모양 그대로다. Ok 가 거짓이면 Error 에 사용자에게 보여 줄 문구가 들어 있다(브라우저 원문이 아니라 이미 사람 말로 옮긴 것).</summary>
internal sealed class LockPasskeyAssertionDto
{
    public bool Ok { get; set; }

    public string? Error { get; set; }

    /// <summary>이 계정에 등록된 기기가 하나도 없어 기기를 부르지도 않았는가. 참이면 단추를 감춘다.</summary>
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
    /// <summary>WebAuthn 을 쓸 수 있는가. http 로 열면 거짓이다(보안 문맥이 아니면 API 가 없다).</summary>
    public bool Supported { get; set; }

    /// <summary>이 기기에 붙박이 인증기(지문·얼굴·Hello)가 있는가.</summary>
    public bool PlatformAuthenticator { get; set; }

    /// <summary>이 기기가 로그인할 때 먼저 묻는 것(password · passkey). 잠금화면은 이 값으로 차례와 단추 무게만 바꾸고, 기기 확인을 스스로 열지는 않는다.</summary>
    public string Priority { get; set; } = "password";
}
