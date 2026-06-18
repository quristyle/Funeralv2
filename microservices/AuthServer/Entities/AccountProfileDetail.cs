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
    public AccountProfileDetail()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Required]
    public string AccountId { get; set; } = string.Empty;

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

    public string? Remark { get; set; }
}
