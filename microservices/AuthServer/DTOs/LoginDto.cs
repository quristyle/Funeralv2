namespace AuthServer.DTOs;

/// <summary>로그인 요청 정보를 담는 DTO</summary>
public class LoginRequestDto
{
    /// <summary>사용자 아이디</summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>사용자 비밀번호</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>로그인 성공 시 반환되는 정보를 담는 DTO</summary>
public class LoginResponseDto
{
    /// <summary>인증용 JWT 액세스 토큰</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>비밀번호 사용 기간이 지났는지. true 라도 토큰은 정상 발급된다 — 비밀번호를 바꾸려면 로그인 상태여야 하므로. 게이트웨이는 변경 경로만 통과시키니 화면은 곧바로 변경으로 안내한다.</summary>
    public bool PasswordExpired { get; set; }

    /// <summary>만료 기준 일수(기본 90). 정책이 꺼져 있으면 null 이다.</summary>
    public int? PasswordExpiryDays { get; set; }

    /// <summary>만료까지 남은 일수. 이미 지났으면 0, 정책이 꺼져 있으면 null 이다.</summary>
    public int? PasswordDaysRemaining { get; set; }
}
