using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.Components.Layout;

/// <summary>지금 로그인한 사람의 이름과 사진. 헤더의 사용자 단추가 쓴다. 쿠키 클레임에는 아이디와 실명만 있고 사진은 계정 확장 속성이라 게이트웨이에 한 번 물어야 한다.
/// 포털관리의 UserInfoDto 를 쓰지 않는다 — 레이아웃은 업무 모듈을 이름으로 알지 못해 필요한 칸만 담은 자기 모양을 쓴다. scoped — 회로 하나가 사용자 한 명이다.</summary>
public sealed class CurrentUser(GatewayClient gateway, ILogger<CurrentUser> logger)
{
    /// <summary>실명. 없으면 로그인 아이디.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>로그인 아이디.</summary>
    public string? Username { get; private set; }

    /// <summary>소속 회사 ID.</summary>
    public string? CompanyId { get; private set; }

    /// <summary>소속 회사명.</summary>
    public string? CompanyName { get; private set; }

    /// <summary>회사 · 부서. 사용자 메뉴 머리에 한 줄로 붙인다.</summary>
    public string? Affiliation { get; private set; }

    /// <summary>보유한 권한 식별자 목록.</summary>
    public List<string> Roles { get; private set; } = [];

    /// <summary>시스템 또는 포털 관리자 여부.</summary>
    public bool IsAdmin => Roles.Contains("ADMINISTRATOR") || Roles.Contains("SYSTEM_ADMINISTRATOR");

    /// <summary>권한 그룹(헤더 로고 옆). 서버가 이미 내려주는 roleNames 로, 식별자가 아니라 사람이 읽는 이름이다. 여럿이면 이어 붙이고 넘치는 것은 CSS 가 자르며 잘린 것은 title 로 보여 준다.</summary>
    public string? RoleText { get; private set; }

    /// <summary>사진 주소. 우리 파일일 때만 값이 있다 — 없으면 Initial 을 그린다.</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>썸네일 사진 주소(150x150 WebP). 헤더 아바타처럼 작은 원형 그림에서 원본 대신 쓴다.</summary>
    public string? AvatarThumbnailUrl { get; private set; }

    /// <summary>한 번이라도 읽었는가.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>로그인한 뒤 처음 열릴 화면의 DB 메뉴 경로(브라우저가 갈 주소가 아니다). 안 골랐으면 서버가 기본값을 채워 보내므로 「골랐다」로 읽지 않는다 — 푸는 곳은 PortalHome.IsUnset 한 곳이다. 여기서는 받은 그대로 든다.</summary>
    public string? HomePath { get; private set; }

    /// <summary>화면에 워터마크를 깔지. 관리자가 정한 값이고 셸이 이것을 보고 깔거나 걷는다. 아직 못 읽었으면 true — 이 표시는 빠지는 쪽이 사고라 모르는 동안은 깔아 두는 것이 맞다.</summary>
    public bool Watermark { get; private set; } = true;

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

    /// <summary>게이트웨이에서 다시 읽는다. 실패는 삼킨다 — 헤더에 얼굴이 안 뜨는 것과 포털이 안 뜨는 것은 무게가 다르다. 못 읽으면 이름만 나온다.</summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Apply(await gateway.GetOneAsync<UserInfoWire>("auth/user/info", cancellationToken));
            return;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "내 정보를 읽지 못했다. 헤더에는 이름만 보인다.");
        }

        Changed?.Invoke();
    }

    /// <summary>이미 받아 둔 값을 채운다. 부트스트랩(PortalBootstrap)이 쓰는 길이라 게이트웨이를 다시 부르지 않는다. null 이면 못 읽은 것으로 보고 그대로 둔다.</summary>
    public void Apply(UserInfoWire? info)
    {
        if (info is not null)
        {
            DisplayName = string.IsNullOrWhiteSpace(info.RealName) ? info.Username : info.RealName;
            Username = info.Username;
            CompanyId = info.CompanyId;
            CompanyName = info.CompanyName;
            Affiliation = Join(info.CompanyName, info.DeptName);
            RoleText = info.RoleNames is { Count: > 0 } names
                ? string.Join(" · ", names.Where(n => !string.IsNullOrWhiteSpace(n)))
                : null;
            Roles = info.Roles ?? [];
            AvatarUrl = OwnFileUrl(info.Avatar);
            AvatarThumbnailUrl = OwnThumbnailUrl(info.Avatar);
            Watermark = info.Watermark;
            HomePath = info.HomePath;
            IsLoaded = true;
        }

        Changed?.Invoke();
    }

    /// <summary>프로필 화면이 사진을 바꾼 뒤 알린다. 다시 조회하지 않는다 — 방금 그 화면이 서버에서 받은 값이다.</summary>
    public void SetAvatar(string? avatar)
    {
        AvatarUrl = OwnFileUrl(avatar);
        AvatarThumbnailUrl = OwnThumbnailUrl(avatar);
        Changed?.Invoke();
    }

    /// <summary>환경설정이 첫 화면을 바꾼 뒤 알린다. 다시 조회하지 않는다(SetAvatar 와 같은 규칙).</summary>
    public void SetHomePath(string? homePath)
    {
        HomePath = homePath;
        Changed?.Invoke();
    }

    /// <summary>사진을 못 받아 왔다. 주소를 버리고 이름 첫 글자로 돌아간다 — 브라우저는 못 받은 그림 자리에 깨진 아이콘을 그려 사진 없는 계정보다 나쁘게 보이고, 사용자가 할 일은 없다. 이유는 인증 끊김·개발 장비에서 올린 파일·삭제 중 무엇이든 화면이 할 일은 같고 다음 새로고침에 다시 시도한다.</summary>
    public void MarkAvatarUnavailable()
    {
        if (AvatarUrl is null && AvatarThumbnailUrl is null)
        {
            return;
        }

        logger.LogInformation(
            "프로필 사진을 표시하지 못했다: {Url}", AvatarThumbnailUrl ?? AvatarUrl);

        AvatarUrl = null;
        AvatarThumbnailUrl = null;
        Changed?.Invoke();
    }

    /// <summary>사진 주소에서 파일 식별자(GUID)를 꺼낸다. 셸 중계 경로와 백엔드 상대경로를 둘 다 해석한다. 규칙은 FileDownload.FileIdOf 로 올라갔다 — 여기 사본을 두면 남의 얼굴을 그리는 화면과 어긋난다.</summary>
    private static string? ExtractFileId(string? avatar) => FileDownload.FileIdOf(avatar);

    /// <summary>우리 파일 주소면 셸 원본 중계 경로로 옮겨 돌려주고, 아니면 null.</summary>
    private static string? OwnFileUrl(string? avatar)
    {
        var id = ExtractFileId(avatar);
        return id is not null ? FileDownload.UrlFor(id) : null;
    }

    /// <summary>우리 파일 주소면 셸 썸네일 중계 경로로 옮겨 돌려주고, 아니면 null. 작은 원형 사진의 용량을 아낀다.</summary>
    private static string? OwnThumbnailUrl(string? avatar)
    {
        var id = ExtractFileId(avatar);
        return id is not null ? FileDownload.ThumbnailUrlFor(id) : null;
    }

    private static string? Join(string? company, string? dept)
    {
        var parts = new[] { company, dept }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>auth/user/info 응답 한 벌(부트스트랩도 같은 모양을 싣는다). 헤더가 쓰는 칸만 담는다.</summary>
    public sealed class UserInfoWire
    {
        public string? Username { get; set; }
        public string? RealName { get; set; }
        public string? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? DeptName { get; set; }
        public string? Avatar { get; set; }

        /// <summary>권한 그룹의 <b>사람이 읽는 이름</b>. 식별자는 <c>Roles</c> 쪽이다.</summary>
        public List<string>? RoleNames { get; set; }

        /// <summary>권한 그룹의 <b>식별자</b>(<c>SYSTEM_ADMINISTRATOR</c>). 회사·부서·사람 셋을 합친 것.</summary>
        public List<string>? Roles { get; set; }

        /// <summary>워터마크를 깔지. 관리자가 정한 값이다.</summary>
        public bool Watermark { get; set; } = true;

        /// <summary>로그인 뒤 처음 열릴 화면(<see cref="CurrentUser.HomePath"/>).</summary>
        public string? HomePath { get; set; }
    }
}
