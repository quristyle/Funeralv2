namespace AuthServer.DTOs;

/// <summary>
/// 소셜 공급자 한 곳의 설정. <c>Auth:Social:Providers:&lt;열쇠&gt;</c> 를 읽는다. 공급자마다 다른 것은 주소 넷과 응답 칸 이름 셋뿐이라 코드에 박지 않는다.
/// 운영에서 채울 것은 ClientId 와 ClientSecret 둘뿐이다(구글·네이버·카카오는 주소가 appsettings.json 에 있다).
/// </summary>
public sealed class SocialProviderOptions
{
    /// <summary>화면에 띄울 이름 — 「구글」 · 「네이버」 · 「카카오」.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 공급자 콘솔에서 받은 앱 아이디. 카카오는 REST API 키다 — JavaScript 키를 넣으면 인가 화면이 KOE101 로 막힌다.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 앱 비밀 열쇠. 커밋하지 않고 scripts/secrets.env 나 appsettings.Local.json 으로만 넣는다. 카카오는 비워 둘 수 있고 구글·네이버는 반드시 있어야 한다.
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
    /// 이메일이 확인된 주소인지 알려 주는 자리. 비어 있으면 확인되지 않은 것으로 본다 — 확인 안 된 이메일로 기존 계정에 붙이는 것이 계정 탈취의 고전적 길이다.
    /// </summary>
    public string EmailVerifiedPath { get; set; } = string.Empty;

    /// <summary>
    /// 프로필 사진 주소가 있는 자리. 주소는 가입 신청과 알림 아이콘으로 쓰고, 바이트는 FileServer 로 옮겨 계정 대표 사진(Avatar)이 된다. 주소를 Avatar 에 그대로 두면 포털이 「사진 없음」으로 본다.
    /// </summary>
    public string PicturePath { get; set; } = string.Empty;

    /// <summary>
    /// 사진을 받아 와도 되는 호스트. 끝이 같으면 된다. 비어 있으면 사진을 옮기지 않는다 — 서버가 바깥 주소를 대신 여는 자리라 목록으로 좁힌다.
    /// </summary>
    public string[] PictureHosts { get; set; } = [];

    /// <summary>
    /// 프로필 응답에서 「사진이 공급자의 <b>기본 그림</b>인가」를 알려 주는 자리
    /// (<c>true</c>/<c>false</c>). 카카오의 <c>kakao_account.profile.is_default_image</c>.
    /// 참이면 사진이 없는 것으로 본다.
    /// </summary>
    public string PictureIsDefaultPath { get; set; } = string.Empty;

    /// <summary>
    /// 공급자의 기본 그림 주소(일부만 맞아도 된다). 알려 주는 칸이 없는 네이버는 이 주소로 가린다 — 그 그림을 대표 사진으로 옮기면 「사진 있음」이 되어 이름 첫 글자 대신 같은 그림자가 선다.
    /// </summary>
    public string[] DefaultPictureUrls { get; set; } = [];

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
    /// 처음 들어온 소셜 계정을 바로 쓰게 할 것인가. 기본은 false — 사내 포털이라 가입 신청의 승인제를 소셜이 우회하면 안 된다.
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// 공급자가 확인했다고 말한 이메일이 기존 계정과 같으면 자동으로 붙일 것인가. 기본은 false. 켜더라도 EmailVerifiedPath 가 참일 때만 붙는다.
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
/// <param name="RedirectUri">공급자가 사람을 돌려보낼 주소. 셸이 정하며 공급자 콘솔 값과 글자 하나까지 같아야 한다.</param>
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

// 못 쓰는 공급자는 가입 화면에서 조용히 사라지므로, 설정 점검이 되도록 못 쓰는 것까지 내보내고 왜 못 쓰는지를 함께 적는다.
// 열쇠가 없어도 화면과 헬스체크는 멀쩡해 보이기 때문이다.

/// <summary>소셜 로그인 설정이 지금 어떤 상태인가. [MSA 서버 상태] 가 읽는다.</summary>
public sealed class SocialConfigStatusDto
{
    /// <summary>기능 스위치(<c>Auth:Social:Enabled</c>). 거짓이면 공급자를 다 채워도 단추가 안 선다.</summary>
    public bool Enabled { get; set; }

    /// <summary>처음 들어온 소셜 계정을 바로 쓰게 하는가. 기본은 거짓(승인제).</summary>
    public bool AutoApprove { get; set; }

    /// <summary>확인된 이메일로 기존 계정에 자동으로 붙이는가. 기본은 거짓.</summary>
    public bool LinkByVerifiedEmail { get; set; }

    /// <summary>지금 실제로 단추가 서는 공급자 수. <b>0 이면 가입 화면에 소셜 칸이 통째로 없다.</b></summary>
    public int UsableCount { get; set; }

    /// <summary>지금까지 연결된 소셜 계정 수. 0 이면 아직 아무도 이 길로 들어오지 않았다.</summary>
    public int LinkedCount { get; set; }

    /// <summary>설정에 적힌 공급자 전부. <b>못 쓰는 것도 들어 있다.</b></summary>
    public List<SocialProviderConfigDto> Providers { get; set; } = [];
}

/// <summary>공급자 하나의 설정 상태. <b>열쇠 값은 절대 담지 않는다</b> — 있는지 없는지만 적는다.</summary>
public sealed class SocialProviderConfigDto
{
    /// <summary>공급자 열쇠 (<c>google</c> …).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>화면에 띄울 이름.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 지금 쓸 수 있는가(<see cref="SocialProviderOptions.IsUsable"/>).
    /// <b>거짓이면 그 공급자는 가입·로그인 화면에 아예 안 나온다.</b>
    /// </summary>
    public bool Usable { get; set; }

    /// <summary><c>ClientId</c> 가 채워져 있는가. 이것이 비어서 못 쓰는 경우가 거의 전부다.</summary>
    public bool HasClientId { get; set; }

    /// <summary>
    /// ClientSecret 이 채워져 있는가. 이것이 비어도 Usable 은 참일 수 있다 — 단추는 서고 토큰을 바꾸는 마지막 걸음에서만 깨져서 증상이 가장 늦게 나타난다.
    /// </summary>
    public bool HasClientSecret { get; set; }

    /// <summary>
    /// 공급자 콘솔에 등록할 콜백 경로. 경로만 준다 — 앞의 origin 은 포털이 안다(AuthServer 는 게이트웨이 뒤라 바깥 주소를 모른다).
    /// </summary>
    public string CallbackPath { get; set; } = string.Empty;

    /// <summary>이 공급자로 연결된 계정 수.</summary>
    public int LinkedCount { get; set; }
}
