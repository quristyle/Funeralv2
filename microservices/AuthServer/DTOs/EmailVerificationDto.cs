namespace AuthServer.DTOs;

/// <summary>이메일 인증코드 발송 요청 DTO</summary>
public class SendEmailVerificationDto
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>이메일 인증코드 검증 요청 DTO</summary>
public class VerifyEmailCodeDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>이메일 인증 결과 DTO</summary>
public class EmailVerificationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? VerificationToken { get; set; }
    public string? DevCode { get; set; }
}
