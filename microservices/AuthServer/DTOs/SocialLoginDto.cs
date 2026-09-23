namespace AuthServer.DTOs;

/// <summary>
/// 소셜 공급자 한 곳의 설정. <c>Auth:Social:Providers:&lt;열쇠&gt;</c> 를 그대로 읽는다.
/// </summary>
/// <remarks>
/// <para>
/// [주소와 칸 이름까지 설정으로 뺀 이유]
/// </para>
///
/// <para>
/// 공급자마다 다른 것은 <b>주소 넷과 응답 JSON 의 칸 이름 셋</b>뿐이고, 흐름은
/// 완전히 같다(인가 → 코드 → 토큰 → 프로필). 그 일곱을 코드에 박으면 「등등」에
/// 해당하는 공급자(애플 · 네이버웍스 · 깃허브 …)를 하나 붙일 때마다 서비스
/// 클래스에 <c>switch</c> 가지가 늘고, 그 가지가 곧 <b>한쪽만 고치는 자리</b>가 된다.
/// 설정으로 두면 공급자 추가가 <c>appsettings.json</c> 한 덩이로 끝난다.
/// </para>
///
/// <para>
/// 그래서 잘 알려진 셋(구글 · 네이버 · 카카오)의 주소·칸 이름은 이미
/// <c>appsettings.json</c> 에 적혀 있다. 운영에서 채워야 하는 것은
/// <see cref="ClientId"/> 와 <see cref="ClientSecret"/> <b>둘뿐이다.</b>
/// </para>
/// </remarks>
public sealed class SocialProviderOptions
{
    /// <summary>화면에 띄울 이름 — 「구글」 · 「네이버」 · 「카카오」.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 공급자 콘솔에서 받은 앱 아이디. 카카오는 <b>REST API 키</b>다
    /// (JavaScript 키가 아니다 — 그걸 넣으면 인가 화면이 <c>KOE101</c> 로 막힌다).
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 앱 비밀 열쇠. <b>절대 커밋하지 않는다</b> — <c>scripts/secrets.env</c> 나
    /// <c>appsettings.Local.json</c> 으로만 넣는다.
    /// <para>
    /// 카카오는 비워 둘 수 있다(콘솔의 [보안 → Client Secret] 을 켜지 않았으면
    /// 애초에 값이 없다). 구글·네이버는 반드시 있어야 한다.
    /// </para>
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>사람을 보내는 인가 화면 주소.</summary>
    public string AuthorizeUrl { get; set; } = string.Empty;

    /// <summary>인가 코드를 access token 으로 바꾸는 주소. <b>서버끼리만 부른다.</b></summary>
    public string TokenUrl { get; set; } = string.Empty;

    /// <summary>프로필을 읽는 주소.</summary>
    public string UserInfoUrl { get; set; } = string.Empty;

    /// <summary>요청할 권한 범위. 빈 값이면 <c>scope</c> 매개변수를 아예 안 붙인다.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// 프로필 응답에서 <b>사용자 번호</b>가 있는 자리. 점으로 내려간다
    /// (네이버는 <c>response.id</c>).
    /// </summary>
    public string IdPath { get; set; } = "id";

    /// <summary>프로필 응답에서 이메일이 있는 자리. 없는 공급자는 비워 둔다.</summary>
    public string EmailPath { get; set; } = string.Empty;

    /// <summary>프로필 응답에서 이름·별명이 있는 자리.</summary>
    public string NamePath { get; set; } = string.Empty;

    /// <summary>
    /// 이메일이 확인된 주소인지를 알려 주는 자리 (<c>true</c>/<c>false</c>).
    /// <b>비어 있으면 「확인되지 않았다」로 본다</b> — 확인되지 않은 이메일로
    /// 기존 계정에 붙이는 것이 계정 탈취의 고전적인 길이라, 모를 때는 안전한
    /// 쪽으로 기운다(<c>Auth:Social:LinkByVerifiedEmail</c> 참고).
    /// </summary>
    public string EmailVerifiedPath { get; set; } = string.Empty;

    /// <summary>
    /// 인가 주소에 덧붙일 매개변수. 구글이 <c>access_type=offline</c> 같은 것을
    /// 요구하거나, 카카오가 <c>prompt=login</c> 을 받는 자리다.
    /// </summary>
    public Dictionary<string, string> ExtraAuthorizeParams { get; set; } = [];

    /// <summary>쓸 수 있는 설정인가. 아이디가 비어 있으면 그 공급자는 없는 셈 친다.</summary>
    public bool IsUsable =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(AuthorizeUrl)
        && !string.IsNullOrWhiteSpace(TokenUrl)
        && !string.IsNullOrWhiteSpace(UserInfoUrl);
}

/// <summary>소셜 로그인 전체 설정. <c>Auth:Social</c>.</summary>
public sealed class SocialLoginOptions
{
    /// <summary>
    /// 기능을 통째로 끈다. <b>되돌리는 길</b>이다 — 코드를 건드리지 않고
    /// 로그인 화면의 단추를 전부 없앨 수 있다.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 처음 들어온 소셜 계정을 <b>바로 쓸 수 있게</b> 할 것인가.
    /// <para>
    /// 기본은 <c>false</c> 다 — 사내 업무 포털이라 아무나 들어와서는 안 되고,
    /// 아이디·비밀번호 가입 신청(<c>/signup</c>)이 이미 승인제다. 소셜만 즉시
    /// 통과시키면 <b>승인제를 우회하는 길</b>이 된다.
    /// </para>
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// 공급자가 <b>확인했다고 말한</b> 이메일이 기존 계정과 같으면 그 계정에
    /// 자동으로 붙일 것인가.
    /// <para>
    /// 기본은 <c>false</c> 다. 켜면 편하지만, 공급자가 이메일을 제대로 확인하지
    /// 않는 순간 <b>남의 이메일로 앱을 하나 만들어 그 사람 계정에 들어가는 길</b>이
    /// 열린다. 켜더라도 <c>EmailVerifiedPath</c> 가 참을 돌려준 경우에만 붙는다.
    /// </para>
    /// </summary>
    public bool LinkByVerifiedEmail { get; set; }

    /// <summary>
    /// 새로 만든 소셜 계정에 적어 둘 첫 화면. 가입 신청이 넣는 값과 같게 둔다.
    /// </summary>
    public string HomePath { get; set; } = "/workspace";

    /// <summary>공급자 목록. 열쇠는 소문자로 둔다(<c>google</c> · <c>naver</c> · <c>kakao</c>).</summary>
    public Dictionary<string, SocialProviderOptions> Providers { get; set; } = [];
}

/// <summary>로그인 화면이 단추를 그리려고 읽는 한 줄.</summary>
/// <param name="Provider">공급자 열쇠 (<c>google</c> …). 시작 주소를 만들 때 쓴다.</param>
/// <param name="DisplayName">단추에 적을 이름.</param>
public record SocialProviderDto(string Provider, string DisplayName);

/// <summary>인가 화면으로 보낼 주소를 만들어 달라는 요청.</summary>
/// <param name="Provider">공급자 열쇠.</param>
/// <param name="RedirectUri">
/// 공급자가 사람을 되돌려 보낼 주소. <b>셸이 정한다</b> — 공급자 콘솔에 등록한
/// 값과 <b>글자 하나까지 같아야</b> 한다.
/// </param>
/// <param name="State">셸이 만든 위조 방지 값. 그대로 돌려받아 대조한다.</param>
public record SocialAuthorizeRequestDto(string Provider, string RedirectUri, string State);

/// <summary>인가 화면 주소.</summary>
/// <param name="Url">브라우저를 이 주소로 보낸다.</param>
public record SocialAuthorizeDto(string Url);

/// <summary>공급자가 되돌려 준 인가 코드로 로그인(또는 가입 신청)한다.</summary>
/// <param name="Provider">공급자 열쇠.</param>
/// <param name="Code">인가 코드. <b>한 번만 쓸 수 있다.</b></param>
/// <param name="RedirectUri">인가를 요청할 때 쓴 것과 <b>같은 값</b>. 다르면 공급자가 거절한다.</param>
/// <param name="State">네이버가 토큰 교환에서도 요구한다. 다른 곳은 안 쓴다.</param>
public record SocialLoginRequestDto(string Provider, string Code, string RedirectUri, string? State);

/// <summary>
/// 소셜로 처음 들어와 <b>가입 신청이 만들어졌을 때</b> 돌려주는 것.
/// 로그인이 된 것이 아니므로 토큰이 없다.
/// </summary>
/// <param name="LoginId">지어 준 로그인 아이디. 나중에 비밀번호로도 들어올 수 있게 알려 준다.</param>
/// <param name="UserName">공급자에게서 받은 이름.</param>
/// <param name="Email">공급자에게서 받은 이메일. 없으면 <c>null</c>.</param>
public record SocialSignupPendingDto(string LoginId, string UserName, string? Email);

/// <summary>「내 정보 → 보안 설정」의 [연결된 소셜 계정] 한 줄.</summary>
public class SocialLinkDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>공급자 열쇠.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>화면에 띄울 공급자 이름. 설정에서 지워진 공급자면 열쇠가 그대로 온다.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>공급자가 알려 준 이름·별명.</summary>
    public string? AccountName { get; set; }

    /// <summary>공급자가 알려 준 이메일.</summary>
    public string? Email { get; set; }

    /// <summary>연결한 시각 (UTC).</summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>마지막으로 이것으로 들어온 시각 (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }
}
