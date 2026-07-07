using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 사용자 계정의 확장 속성 (이메일, 전화번호, 사진 등)
/// </summary>
[Table("account_profile_details", Schema = "scom")]
public class AccountProfileDetail : BaseEntity<string>
{
    /// <summary>
    /// AccountProfileDetail 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public AccountProfileDetail()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 연관된 사용자 계정 식별자 (ID)
    /// </summary>
    [Required]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 사용자 계정 엔티티 탐색 속성
    /// </summary>
    [ForeignKey("AccountId")]
    public Account? Account { get; set; }

    /// <summary>
    /// 속성 유형 (Email, Phone, Fax, Photo, SNS 등)
    /// </summary>
    [Required]
    public string DetailType { get; set; } = string.Empty;

    /// <summary>
    /// 실제 데이터 값
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 대표 여부 (여러 이메일 중 기본값 등)
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// 라벨 (회사, 개인, 집 등)
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    public string? Remark { get; set; }
}
