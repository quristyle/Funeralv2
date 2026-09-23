using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 계정 하나에 연결된 <b>소셜 계정 한 개</b>.
/// 구글 · 네이버 · 카카오가 모두 여기 한 줄로 남는다.
/// </summary>
/// <remarks>
/// <para>
/// [비밀이 들어 있지 않다]
/// </para>
///
/// <para>
/// 여기 담는 것은 <b>공급자가 지어 준 사용자 번호</b>(<see cref="ProviderUserId"/>)와
/// 화면에 띄울 이름·이메일뿐이다. 소셜 쪽 access token 은 <b>저장하지 않는다</b> —
/// 우리가 쓰는 곳은 로그인 직후 프로필 한 번 읽는 자리뿐이라 들고 있을 이유가
/// 없고, 들고 있으면 이 표가 「남의 구글 계정을 대신 부를 수 있는 열쇠 꾸러미」가
/// 된다. 패스키 표(<see cref="AccountWebAuthnCredential"/>)와 같은 취지다.
/// </para>
///
/// <para>
/// [<see cref="ProviderUserId"/> 로 찾지 이메일로 찾지 않는다]
/// </para>
///
/// <para>
/// 이메일은 사람이 바꿀 수 있고 공급자에 따라 <b>아예 안 주기도 한다</b>(카카오는
/// 동의 항목을 켜지 않으면 비어서 온다). 그 값으로 주인을 찾으면 이메일을 바꾼
/// 순간 남의 계정으로 들어가거나, 자기 계정을 잃어버린다. 공급자가 지어 준
/// 번호는 그 계정이 살아 있는 한 변하지 않으므로 그것 하나만 열쇠로 쓴다.
/// </para>
///
/// <para>
/// [끊는 것은 사람이 한다]
/// </para>
///
/// <para>
/// 「내 정보 → 보안 설정」의 [연결된 소셜 계정] 에서 뗀다. 계정이 사라지면
/// 함께 지운다(외래 키 Cascade).
/// </para>
/// </remarks>
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
    /// 공급자가 지어 준 사용자 번호. <b>로그인은 이 값으로 주인을 찾는다</b>
    /// (머리말 참고). 공급자 안에서만 고유하므로 <see cref="Provider"/> 와 짝으로 쓴다.
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
