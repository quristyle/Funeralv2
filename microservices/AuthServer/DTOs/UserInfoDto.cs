namespace AuthServer.DTOs;

/// <summary>
/// 프론트엔드 UserInfo 타입에 대응하는 사용자 정보 DTO
/// </summary>
public class UserInfoDto
{
    /// <summary>사용자 고유 식별자</summary>
    public string? Id { get; set; }
    /// <summary>사용자 ID</summary>
    public string? UserId { get; set; }

    /// <summary>로그인 아이디</summary>
    public string? Username { get; set; }

    /// <summary>사용자 실명</summary>
    public string? RealName { get; set; }

    /// <summary>소속 회사명</summary>
    public string? CompanyName { get; set; }

    /// <summary>부서명</summary>
    public string? DeptName { get; set; }

    /// <summary>아바타 이미지 URL</summary>
    public string Avatar { get; set; } = "https://gw.alipayobjects.com/zos/antfincdn/XAosXuNZyF/BiazfanxmamNRoxxVxka.png";

    /// <summary>아바타 이미지 파일 그룹 ID</summary>
    public string? AvatarGroupId { get; set; }

    /// <summary>사용자 설명 또는 이메일</summary>
    public string? Desc { get; set; }

    /// <summary>로그인 후 리다이렉트될 기본 홈 경로</summary>
    public string HomePath { get; set; } = "/workspace";

    /// <summary>사용자가 보유한 권한(Role) 식별자 목록 (ADMINISTRATOR 등)</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// 사용자가 보유한 권한(Role) 표시 이름 목록 (관리자, 시스템관리자 …).
    /// 화면에 역할을 보여 줄 때 쓴다 — 식별자는 사람이 읽기 어렵다.
    /// </summary>
    public List<string> RoleNames { get; set; } = new();

    /// <summary>
    /// 이 계정이 어느 MSA 레코드에서 만들어졌는지. 형식은 <c>&lt;서비스&gt;:&lt;테이블&gt;:&lt;원본키&gt;</c> 다.
    /// (<c>helpdesk:admin:4</c> · <c>projmng:dev_user:jskim</c>)
    ///
    /// <para>
    /// 이관 계정은 아이디 충돌을 피하려고 접두어가 붙어 있다(<c>jskim</c> → <c>pm_jskim</c>).
    /// 그래서 화면이 포털 로그인 아이디로 저쪽 레코드를 찾으면 아무것도 못 찾는다.
    /// 어느 레코드에서 왔는지는 포털만 알고 있으므로 여기서 함께 내려준다.
    /// </para>
    ///
    /// <para>이관으로 만들어진 계정만 값이 있다. 원래부터 있던 계정은 null 이다.</para>
    /// </summary>
    public string? MsaSource { get; set; }

    /// <summary>추가 토큰 정보 (필요 시)</summary>
    public string Token { get; set; } = string.Empty;

    // 프로필 확장 필드 (기본 설정, 보안 설정, 알림 설정 값 바인딩용)
    public string? Introduction { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    
    public bool SecurityPhone { get; set; }
    public bool SecurityQuestion { get; set; }
    public bool SecurityEmail { get; set; }
    public bool SecurityMfa { get; set; }
    
    public bool SystemMessage { get; set; }
    public bool TodoTask { get; set; }
    public bool AccountPasswordNotify { get; set; }
}
