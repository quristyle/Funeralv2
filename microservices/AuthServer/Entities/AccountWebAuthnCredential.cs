using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 계정 하나에 등록된 <b>패스키(WebAuthn 인증기) 한 개</b>.
/// 휴대폰 지문·얼굴, 윈도우 Hello, 보안 열쇠가 모두 여기 한 줄로 남는다.
/// </summary>
/// <remarks>
/// <para>
/// [비밀이 들어 있지 않다]
/// </para>
///
/// <para>
/// 여기 저장하는 것은 <b>공개 키</b>다. 지문 자체도, 그것을 풀 개인 키도
/// 기기 밖으로 나오지 않는다 — 그것이 비밀번호와 갈리는 지점이고, 그래서
/// 이 표를 통째로 읽은 사람도 남의 계정으로 로그인할 수 없다.
/// 비밀번호 표(<c>accounts.password</c>)와 달리 해시조차 필요 없다.
/// </para>
///
/// <para>
/// [서명 횟수(<see cref="SignCount"/>)를 왜 들고 있나]
/// </para>
///
/// <para>
/// 인증기가 서명할 때마다 올려 주는 숫자다. 받은 값이 저장값보다 <b>크지
/// 않으면</b> 인증기가 복제됐다는 신호다(같은 개인 키를 두 기기가 들고 있다).
/// 다만 <b>0 을 늘 주는 인증기가 많다</b> — 애플 패스키가 그렇다. 그래서
/// 둘 다 0 인 경우는 정상으로 본다. 규격(WebAuthn §6.1.1)도 그렇게 적는다.
/// </para>
///
/// <para>
/// [지우는 것은 사람이 한다]
/// </para>
///
/// <para>
/// 기기를 잃어버리면 이 줄을 지워야 그 기기로 못 들어온다. 그 길이
/// 「내 정보 → 보안 설정」에 있다. 계정이 사라지면 함께 지운다.
/// </para>
/// </remarks>
[Table("account_webauthn_credentials", Schema = "scom")]
public class AccountWebAuthnCredential : BaseEntity<string>
{
    public AccountWebAuthnCredential()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>이 패스키의 주인.</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>계정 엔티티 탐색 속성.</summary>
    [ForeignKey("AccountId")]
    public Account? Account { get; set; }

    /// <summary>
    /// 인증기가 지어 준 자격 증명 아이디 (base64url).
    /// 로그인할 때 브라우저가 이 값을 들고 오므로 <b>여기로 찾는다.</b>
    /// </summary>
    [Required]
    [Column("credential_id")]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>
    /// 공개 키. <c>SubjectPublicKeyInfo</c> DER 을 base64 로 담는다.
    /// <para>
    /// COSE 형식 그대로가 아니라 SPKI 로 두는 이유는 .NET 이 그 형식을
    /// 바로 읽기 때문이다(<c>ECDsa.ImportSubjectPublicKeyInfo</c>). COSE 를
    /// 담으면 검증할 때마다 CBOR 을 풀어야 하고, 그 코드가 곧
    /// <b>서명 검증에서 가장 틀리기 쉬운 자리</b>가 된다.
    /// </para>
    /// </summary>
    [Required]
    [Column("public_key")]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// 서명 알고리즘 (COSE 식별자). <c>-7</c> = ES256, <c>-257</c> = RS256.
    /// 검증할 때 어느 열쇠 형식으로 읽을지를 이 값이 정한다.
    /// </summary>
    [Column("algorithm")]
    public int Algorithm { get; set; }

    /// <summary>서명 횟수. 머리말의 복제 감지에 쓴다.</summary>
    [Column("sign_count")]
    public long SignCount { get; set; }

    /// <summary>
    /// 사용자가 알아볼 이름. 「아이폰 지문」처럼 사람이 붙인다.
    /// 기기가 여럿이면 <b>어느 줄을 지워야 하는지</b>를 이 이름으로만 알 수 있다.
    /// </summary>
    [Column("label")]
    public string? Label { get; set; }

    /// <summary>
    /// 인증기 종류 — <c>platform</c>(기기에 붙박이: 지문·얼굴) ·
    /// <c>cross-platform</c>(따로 꽂는 보안 열쇠).
    /// </summary>
    [Column("attachment")]
    public string? Attachment { get; set; }

    /// <summary>마지막으로 이 패스키로 들어온 시각 (UTC). 한 번도 안 썼으면 <c>null</c>.</summary>
    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }
}
