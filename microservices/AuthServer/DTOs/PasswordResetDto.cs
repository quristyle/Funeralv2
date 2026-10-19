namespace AuthServer.DTOs;

/// <summary>비밀번호 재설정 링크를 보내 달라는 요청(익명). 이메일과 아이디를 함께 받는다 — 한 사람이 계정을 둘 이상 가진 경우가 있어 이메일만으로는 계정을 정할 수 없고, 두 값이 맞아야 링크가 나간다.</summary>
public class ForgotPasswordDto
{
    public string LoginId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>메일로 받은 링크로 비밀번호를 다시 정하는 요청 (익명).</summary>
public class ResetPasswordDto
{
    /// <summary>메일 링크의 <c>token</c> 질의값 원문.</summary>
    public string Token { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
