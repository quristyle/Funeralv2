using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 인증 및 권한 관련 비즈니스 로직 인터페이스
/// </summary>
public interface IAuthService
{
    /// <summary>로그인 검증 및 토큰 발급</summary>
    /// <param name="request">로그인 요청 정보</param>
    /// <returns>로그인 성공 시 응답 DTO, 실패 시 null</returns>
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);

    /// <summary>로그아웃 처리</summary>
    Task LogoutAsync();
}
