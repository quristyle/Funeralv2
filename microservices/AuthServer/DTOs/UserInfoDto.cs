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

    /// <summary>아바타 이미지 URL</summary>
    public string Avatar { get; set; } = "https://gw.alipayobjects.com/zos/antfincdn/XAosXuNZyF/BiazfanxmamNRoxxVxka.png";

    /// <summary>사용자 설명 또는 이메일</summary>
    public string? Desc { get; set; }

    /// <summary>로그인 후 리다이렉트될 기본 홈 경로</summary>
    public string HomePath { get; set; } = "/dashboard/workspace";

    /// <summary>사용자가 보유한 권한(Role) 목록</summary>
    public List<string> Roles { get; set; } = new();

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
