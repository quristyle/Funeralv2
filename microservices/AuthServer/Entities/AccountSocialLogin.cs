using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 계정 하나에 연결된 <b>소셜 계정 한 개</b>.
/// 구글 · 네이버 · 카카오가 모두 여기 한 줄로 남는다.
/// </summary>
/// 비밀은 저장하지 않는다 — 공급자가 지어 준 사용자 번호와 표시용 이름·이메일뿐이다. access token 을 들고 있으면 남의 계정을 대신 부를 열쇠가 된다.
/// 주인은 ProviderUserId 로 찾는다 — 이메일은 사람이 바꾸거나 공급자가 아예 안 주므로 열쇠로 쓰면 남의 계정으로 들어갈 수 있다. 끊는 것은 「내 정보 → 보안 설정」에서 사람이 한다.
[Table("account_social_logins", Schema = "scom")]
public class AccountSocialLogin : BaseEntity<string>
{
    public AccountSocialLogin()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>이 소셜 연결의 주인.</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>계정 엔티티 탐색 속성.</summary>
    [ForeignKey("AccountId")]
    public Account? Account { get; set; }

    /// <summary>
    /// 공급자 열쇠 — <c>google</c> · <c>naver</c> · <c>kakao</c>.
    /// 설정(<c>Auth:Social:Providers</c>)의 칸 이름과 <b>같아야 한다.</b>
    /// </summary>
    [Required]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 로그인은 이 값으로 주인을 찾는다. 공급자 안에서만 고유하므로 Provider 와 짝으로 쓴다.
    /// </summary>
    [Required]
    [Column("provider_user_id")]
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// 공급자가 알려 준 이메일. <b>참고용이다</b> — 이 값으로 주인을 찾지 않는다.
    /// 공급자가 안 주면 비어 있다.
    /// </summary>
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>
    /// 공급자가 알려 준 이름·별명. 「연결된 소셜 계정」 목록에서 <b>어느 줄을
    /// 떼어야 하는지</b>를 이 값으로 알아본다.
    /// </summary>
    [Column("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>마지막으로 이 소셜 계정으로 들어온 시각 (UTC). 한 번도 안 썼으면 <c>null</c>.</summary>
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
}
