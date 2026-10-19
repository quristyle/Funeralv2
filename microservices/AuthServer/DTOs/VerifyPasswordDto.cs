namespace AuthServer.DTOs;

/// <summary>잠금화면이 보내는 비밀번호 확인 요청.</summary>
/// ChangePasswordDto 를 쓰지 않는다 — 새 비밀번호 칸이 딸려 오면 확인만 하는 자리에서 바꿀 수 있는 것처럼 읽힌다.
public class VerifyPasswordDto
{
    public string Password { get; set; } = string.Empty;
}
