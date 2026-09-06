namespace AuthServer.DTOs;

/// <summary>
/// 잠금화면이 보내는 비밀번호 확인 요청 (D7).
/// </summary>
/// <remarks>
/// <see cref="ChangePasswordDto"/> 를 재사용하지 않는 이유: 그쪽에는
/// <c>NewPassword</c> 가 있어서, 확인만 하는 자리에 새 비밀번호 칸이
/// 딸려 다니면 다음 사람이 「여기서도 바꿀 수 있나」로 읽는다.
/// </remarks>
public class VerifyPasswordDto
{
    public string Password { get; set; } = string.Empty;
}
