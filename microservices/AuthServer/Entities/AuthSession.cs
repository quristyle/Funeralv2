using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>로그인 한 번이 만든 세션. 접근·갱신 토큰이 모두 <c>SessionId</c> 클레임으로 이 줄을 가리킨다.</summary>
/// 토큰은 서명만으로 믿으므로 되돌릴 자리가 따로 있어야 한다. 로그아웃·비밀번호 변경·계정 정지가 이 줄을 닫으면
/// 갱신이 거절되고, 이미 나간 접근 토큰은 짧은 수명(기본 15분) 안에 죽는다.
[Table("auth_sessions", Schema = "scom")]
public class AuthSession : BaseEntity<string>
{
    public AuthSession()
    {
        Id = Guid.NewGuid().ToString("N");
    }

    /// <summary>세션의 주인.</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [ForeignKey(nameof(AccountId))]
    public Account? Account { get; set; }

    /// <summary>이 세션이 끝나는 시각(UTC). 갱신해도 늘지 않는다 — 로그인 한 번이 사는 최대 길이다.</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>마지막으로 갱신한 시각(UTC).</summary>
    [Column("last_refreshed_at")]
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>닫힌 시각(UTC). 비어 있으면 살아 있다.</summary>
    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>닫힌 까닭(LOGOUT · PASSWORD_CHANGED · PASSWORD_RESET · ADMIN_PASSWORD_RESET).</summary>
    [Column("revoke_reason")]
    public string? RevokeReason { get; set; }

    /// <summary>로그인한 IP.</summary>
    [Column("ip")]
    public string? Ip { get; set; }

    /// <summary>로그인한 브라우저·기기.</summary>
    [Column("user_agent")]
    public string? UserAgent { get; set; }
}

/// <summary>세션을 닫은 까닭.</summary>
public static class AuthSessionRevokeReason
{
    public const string Logout = "LOGOUT";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string AdminPasswordReset = "ADMIN_PASSWORD_RESET";
}
