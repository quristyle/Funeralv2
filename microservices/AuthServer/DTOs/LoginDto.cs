namespace AuthServer.DTOs;

/// <summary>
/// 로그인 요청 정보를 담는 DTO
/// </summary>
public class LoginRequestDto
{
    /// <summary>사용자 아이디</summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>사용자 비밀번호</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 로그인 성공 시 반환되는 정보를 담는 DTO
/// </summary>
public class LoginResponseDto
{
    /// <summary>인증용 JWT 액세스 토큰</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 비밀번호 사용 기간이 지났는지.
    /// <para>
    /// true 라도 토큰은 정상 발급된다 — 비밀번호를 바꾸려면 로그인 상태여야 하기 때문이다.
    /// 대신 게이트웨이가 비밀번호 변경에 필요한 경로만 통과시키므로,
    /// 화면은 이 값을 보고 곧바로 비밀번호 변경으로 안내해야 한다.
    /// </para>
    /// </summary>
    public bool PasswordExpired { get; set; }

    /// <summary>만료 기준 일수(기본 90). 정책이 꺼져 있으면 null 이다.</summary>
    public int? PasswordExpiryDays { get; set; }

    /// <summary>만료까지 남은 일수. 이미 지났으면 0, 정책이 꺼져 있으면 null 이다.</summary>
    public int? PasswordDaysRemaining { get; set; }
}
