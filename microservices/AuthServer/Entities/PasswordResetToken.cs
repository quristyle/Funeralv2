using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>비밀번호 재설정 링크 한 장.</summary>
/// 메일로 보낸 토큰은 비밀번호와 같은 무게라 SHA-256 해시만 저장한다. 토큰은 256비트 난수라 PBKDF2 를 쓰지 않는다.
/// 쓴 토큰은 지우지 않고 표시만 한다 — 지우면 「이미 쓴 링크」와 「없는 링크」를 구분할 수 없다.
[Table("password_reset_tokens", Schema = "scom")]
public class PasswordResetToken : BaseEntity<string>
{
    public PasswordResetToken()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>이 링크가 가리키는 계정.</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>계정 엔티티 탐색 속성.</summary>
    [ForeignKey("AccountId")]
    public Account? Account { get; set; }

    /// <summary>토큰 원문의 SHA-256 해시 (base64). <b>원문은 어디에도 없다.</b></summary>
    [Required]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>만료 시각 (UTC).</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>실제로 비밀번호를 바꾼 시각 (UTC). 아직 안 썼으면 <c>null</c>.</summary>
    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    /// <summary>요청이 들어온 IP. 누가 남의 계정으로 링크를 뿌리는지 볼 수 있게 남긴다.</summary>
    [Column("request_ip")]
    public string? RequestIp { get; set; }
}
