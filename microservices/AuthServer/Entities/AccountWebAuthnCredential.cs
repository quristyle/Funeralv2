using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>계정 하나에 등록된 패스키(WebAuthn 인증기) 한 개. 지문·얼굴·보안 열쇠가 모두 여기 한 줄로 남는다.</summary>
/// 공개 키만 저장하므로 비밀이 없다. SignCount 는 0 을 늘 주는 인증기가 있어 둘 다 0 이면 정상이다. 지우는 것은 사람이 한다.
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

    /// <summary>인증기가 지어 준 자격 증명 아이디(base64url). 로그인 때 이 값으로 주인을 찾는다.</summary>
    [Required]
    [Column("credential_id")]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>공개 키. SubjectPublicKeyInfo DER 을 base64 로 담는다.</summary>
    /// COSE 가 아니라 SPKI 로 두는 까닭은 .NET 이 바로 읽기 때문이다 — COSE 면 검증마다 CBOR 을 풀어야 한다.
    [Required]
    [Column("public_key")]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>서명 알고리즘(COSE 식별자). -7 = ES256, -257 = RS256. 이 값이 검증할 열쇠 형식을 정한다.</summary>
    [Column("algorithm")]
    public int Algorithm { get; set; }

    /// <summary>서명 횟수. 머리말의 복제 감지에 쓴다.</summary>
    [Column("sign_count")]
    public long SignCount { get; set; }

    /// <summary>사용자가 알아볼 이름. 「아이폰 지문」처럼 사람이 붙인다.</summary>
    [Column("label")]
    public string? Label { get; set; }

    /// <summary>인증기 종류 — platform(기기 붙박이) · cross-platform(따로 꽂는 보안 열쇠).</summary>
    [Column("attachment")]
    public string? Attachment { get; set; }

    /// <summary>마지막으로 이 패스키로 들어온 시각 (UTC). 한 번도 안 썼으면 <c>null</c>.</summary>
    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }
}
