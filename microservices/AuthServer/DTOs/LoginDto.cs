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
}
