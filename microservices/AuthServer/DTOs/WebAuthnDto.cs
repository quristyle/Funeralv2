namespace AuthServer.DTOs;

/// <summary>
/// 브라우저에 그대로 넘겨 줄 <c>navigator.credentials</c> 설정과, 그것을
/// 나중에 대조하기 위한 <b>표 번호</b>.
/// </summary>
/// <remarks>
/// 도전값(challenge)을 브라우저에만 맡길 수는 없다 — 그러면 아무 값이나
/// 지어내 서명해 올 수 있다. 서버가 자기 쪽에도 5분짜리로 적어 두고
/// <see cref="SessionId"/> 로 다시 찾는다. 그 보관은
/// <c>WebAuthnService</c> 가 <c>IMemoryCache</c> 로 한다.
/// </remarks>
public class WebAuthnOptionsDto
{
    /// <summary>이 도전값을 다시 찾을 번호. 다음 요청에 그대로 실어 보낸다.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 브라우저가 받는 설정. <c>PublicKeyCredentialCreationOptions</c> 또는
    /// <c>PublicKeyCredentialRequestOptions</c> 의 JSON 모양 그대로다
    /// (이진값은 base64url 문자열이고, JS 쪽에서 바이트로 되돌린다).
    /// </summary>
    public Dictionary<string, object?> PublicKey { get; set; } = [];
}

/// <summary>패스키 등록 요청 — 브라우저의 <c>navigator.credentials.create()</c> 결과.</summary>
public class WebAuthnRegisterDto
{
    /// <summary>등록 설정을 받을 때 받은 번호.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>자격 증명 아이디 (base64url).</summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>
    /// 공개 키 (base64, SPKI DER). 브라우저의
    /// <c>response.getPublicKey()</c> 가 주는 값이다.
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>서명 알고리즘 (COSE 식별자). <c>response.getPublicKeyAlgorithm()</c>.</summary>
    public int Algorithm { get; set; }

    /// <summary>인증기 자료 (base64url). <c>response.getAuthenticatorData()</c>.</summary>
    public string AuthenticatorData { get; set; } = string.Empty;

    /// <summary>브라우저가 서명 대상에 넣은 문맥 (base64url). 도전값·오리진이 들어 있다.</summary>
    public string ClientDataJson { get; set; } = string.Empty;

    /// <summary>사람이 붙인 이름. 비면 서버가 기기를 보고 지어 준다.</summary>
    public string? Label { get; set; }

    /// <summary><c>platform</c> · <c>cross-platform</c>.</summary>
    public string? Attachment { get; set; }
}

/// <summary>패스키 로그인 요청 — 브라우저의 <c>navigator.credentials.get()</c> 결과.</summary>
public class WebAuthnLoginDto
{
    /// <summary>로그인 설정을 받을 때 받은 번호.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>어느 패스키로 서명했는지 (base64url).</summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>인증기 자료 (base64url).</summary>
    public string AuthenticatorData { get; set; } = string.Empty;

    /// <summary>서명 문맥 (base64url).</summary>
    public string ClientDataJson { get; set; } = string.Empty;

    /// <summary>서명값 (base64url).</summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// 인증기가 알려 준 계정 손잡이 (base64url). 아이디를 치지 않고 들어올 때
    /// <b>누구인지 말해 주는 유일한 값</b>이다. 없을 수도 있다.
    /// </summary>
    public string? UserHandle { get; set; }
}

/// <summary>「내 정보 → 보안 설정」의 패스키 목록 한 줄.</summary>
public class WebAuthnCredentialDto
{
    /// <summary>줄 식별자. 지울 때 쓴다.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>사람이 붙인 이름.</summary>
    public string? Label { get; set; }

    /// <summary><c>platform</c> · <c>cross-platform</c>.</summary>
    public string? Attachment { get; set; }

    /// <summary>등록 시각 (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>마지막 사용 시각 (UTC). 한 번도 안 썼으면 <c>null</c>.</summary>
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>패스키 이름만 고친다.</summary>
public class WebAuthnRenameDto
{
    /// <summary>새 이름.</summary>
    public string Label { get; set; } = string.Empty;
}
