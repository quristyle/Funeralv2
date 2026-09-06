using System.Text.RegularExpressions;
using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 지금 로그인한 사람의 <b>이름과 사진</b>. 헤더의 사용자 단추가 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// [<c>AuthenticationState</c> 로는 부족하다]
/// </para>
///
/// <para>
/// 쿠키의 클레임에는 로그인 아이디와 실명까지만 들어 있다. 사진은 계정
/// 확장 속성(<c>account_profile_details</c> 의 <c>Avatar</c>)이라 게이트웨이에
/// 물어야 나온다. 헤더에 얼굴을 띄우려면 그 한 번의 조회가 필요하다.
/// </para>
///
/// <para>
/// [포털관리의 <c>UserInfoDto</c> 를 쓰지 않는다]
/// </para>
///
/// <para>
/// 헤더는 <b>레이아웃</b>이고 레이아웃은 업무 모듈을 이름으로 알지 못한다
/// (web/CLAUDE.md). 그래서 필요한 네 칸만 담은 자기 모양을 쓴다. 셸의
/// <c>PasswordChange</c> 가 같은 이유로 자기 <c>PasswordStatus</c> 를 들고 있다.
/// </para>
///
/// <para>
/// [사진 주소를 그대로 쓰지 않는다 — 두 가지를 옮긴다]
/// </para>
///
/// <para>
/// ① <c>/api/file/download/{id}</c> 는 <b>Vue 시절 주소</b>다. 그때는 브라우저가
/// 게이트웨이와 같은 오리진이었지만 지금 브라우저가 보는 것은 포털(:5557)이고
/// 거기에는 <c>/api</c> 가 없다 — 그대로 쓰면 사진 자리가 깨진 그림이 된다.
/// <see cref="FileDownload.UrlFor"/> 로 셸 중계 경로로 옮긴다.
/// </para>
///
/// <para>
/// ② 사진이 없는 계정에는 서버가 <b>바깥 기본 이미지 주소</b>를 채워 준다
/// (alipayobjects.com). 그것을 그대로 걸면 화면을 열 때마다 바깥으로 요청이
/// 나가고, 이 저장소는 <b>런타임에 CDN 을 부르지 않는다</b>(web/CLAUDE.md 의
/// Bootstrap 항목과 같은 이유다). 우리 파일이 아닌 주소는 「사진 없음」으로
/// 보고 이름 첫 글자를 대신 그린다.
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 사용자 한 명이다.
/// </para>
/// </remarks>
public sealed partial class CurrentUser(GatewayClient gateway, ILogger<CurrentUser> logger)
{
    /// <summary>실명. 없으면 로그인 아이디.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>로그인 아이디.</summary>
    public string? Username { get; private set; }

    /// <summary>회사 · 부서. 사용자 메뉴 머리에 한 줄로 붙인다.</summary>
    public string? Affiliation { get; private set; }

    /// <summary>
    /// 사진 주소. <b>우리 파일일 때만 값이 있다</b> — 없으면 <see cref="Initial"/> 을 그린다.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>한 번이라도 읽었는가.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>이름·사진이 바뀌었다. 헤더가 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>사진이 없을 때 동그라미에 넣을 글자 한 자.</summary>
    public string Initial
    {
        get
        {
            var source = DisplayName ?? Username;
            return string.IsNullOrWhiteSpace(source) ? "?" : source.Trim()[..1].ToUpperInvariant();
        }
    }

    /// <summary>
    /// 게이트웨이에서 다시 읽는다.
    /// </summary>
    /// <remarks>
    /// <b>실패를 삼킨다.</b> 헤더에 얼굴이 안 뜨는 것과 포털이 안 뜨는 것의
    /// 무게가 다르다 — 못 읽으면 이름만 나온다.
    /// </remarks>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await gateway.GetOneAsync<Payload>("auth/user/info", cancellationToken);

            if (info is not null)
            {
                DisplayName = string.IsNullOrWhiteSpace(info.RealName) ? info.Username : info.RealName;
                Username = info.Username;
                Affiliation = Join(info.CompanyName, info.DeptName);
                AvatarUrl = OwnFileUrl(info.Avatar);
                IsLoaded = true;
            }
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "내 정보를 읽지 못했다. 헤더에는 이름만 보인다.");
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// 프로필 화면이 사진을 바꾼 뒤 알려 준다. <b>다시 조회하지 않는다</b> —
    /// 방금 그 화면이 서버에서 받은 값이라 한 번 더 물을 이유가 없다.
    /// </summary>
    public void SetAvatar(string? avatar)
    {
        AvatarUrl = OwnFileUrl(avatar);
        Changed?.Invoke();
    }

    /// <summary>
    /// 사진을 못 받아 왔다. <b>주소를 버리고 이름 첫 글자로 돌아간다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 브라우저가 그림을 못 받으면 그 자리에 <b>깨진 그림 아이콘</b>을 그린다.
    /// 헤더에 그것이 뜨면 사진이 없는 계정(첫 글자가 뜨는 쪽)보다 더 나쁘게
    /// 보이고, 사용자가 할 수 있는 일은 없다.
    /// </para>
    /// <para>
    /// 못 받는 이유가 하나가 아니다 — 인증이 잠깐 끊겼거나, 개발 장비에서
    /// 올린 파일이라 운영에 바이트가 없거나(루트 CLAUDE.md), 누가 그 파일을
    /// 지웠거나. <b>어느 쪽이든 화면이 할 일은 같다.</b> 다음 새로고침에
    /// 다시 시도한다.
    /// </para>
    /// </remarks>
    public void MarkAvatarUnavailable()
    {
        if (AvatarUrl is null)
        {
            return;
        }

        logger.LogInformation("프로필 사진을 표시하지 못했다: {Url}", AvatarUrl);

        AvatarUrl = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// 우리 파일 주소면 셸 중계 경로로 옮겨 돌려주고, 아니면 <c>null</c>.
    /// 왜 바깥 주소를 버리는지는 이 클래스 머리말에 있다.
    /// </summary>
    private static string? OwnFileUrl(string? avatar)
    {
        if (string.IsNullOrWhiteSpace(avatar))
        {
            return null;
        }

        // 이미 셸 중계 경로면 그대로 쓴다.
        if (avatar.StartsWith(FileDownload.Path + "/", StringComparison.OrdinalIgnoreCase))
        {
            return avatar;
        }

        var match = LegacyFileUrl().Match(avatar);
        return match.Success ? FileDownload.UrlFor(match.Groups["id"].Value) : null;
    }

    private static string? Join(string? company, string? dept)
    {
        var parts = new[] { company, dept }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// Vue 시절 첨부 주소. <c>/api/file/download/{guid}</c> 와
    /// <c>/api/file/download/id/{guid}</c> 둘 다 남아 있어 <c>id/</c> 를 선택으로 둔다.
    /// </summary>
    [GeneratedRegex(
        @"/api/file/download/(?:id/)?(?<id>[0-9a-fA-F-]{36})",
        RegexOptions.IgnoreCase)]
    private static partial Regex LegacyFileUrl();

    /// <summary><c>auth/user/info</c> 에서 헤더가 쓰는 칸만.</summary>
    private sealed class Payload
    {
        public string? Username { get; set; }
        public string? RealName { get; set; }
        public string? CompanyName { get; set; }
        public string? DeptName { get; set; }
        public string? Avatar { get; set; }
    }
}
