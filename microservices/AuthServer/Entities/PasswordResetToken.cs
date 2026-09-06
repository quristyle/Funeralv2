using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 비밀번호 재설정 링크 한 장.
/// </summary>
/// <remarks>
/// <para>
/// [원문을 저장하지 않는다]
/// </para>
///
/// <para>
/// 메일로 보낸 토큰은 <b>그 자체가 비밀번호와 같은 무게</b>다. 들고 있으면
/// 남의 비밀번호를 바꿀 수 있다. 그래서 DB 에는 SHA-256 해시만 남기고 원문은
/// 메일로 나간 뒤 서버에서 사라진다. DB 를 통째로 읽은 사람도 링크를 만들어
/// 낼 수 없다.
/// </para>
///
/// <para>
/// 비밀번호와 달리 PBKDF2 를 쓰지 않는다. 토큰은 사람이 정한 값이 아니라
/// 256비트 난수라 사전 공격의 대상이 아니고, 늘려 봐야 검증만 느려진다.
/// </para>
///
/// <para>
/// [쓴 토큰을 지우지 않고 표시만 한다]
/// </para>
///
/// <para>
/// 지우면 「이미 쓴 링크」와 「없는 링크」가 구분되지 않아, 두 번 누른
/// 사람에게 무슨 일이 있었는지 말해 줄 수 없다. 오래된 것은 청소로 지운다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 토큰 원문의 SHA-256 해시 (base64). <b>원문은 어디에도 없다.</b>
    /// </summary>
    [Required]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>만료 시각 (UTC).</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>실제로 비밀번호를 바꾼 시각 (UTC). 아직 안 썼으면 <c>null</c>.</summary>
    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// 요청이 들어온 아이피. 누가 남의 계정으로 링크를 뿌리고 있는지
    /// 나중에 볼 수 있어야 한다.
    /// </summary>
    [Column("request_ip")]
    public string? RequestIp { get; set; }
}
