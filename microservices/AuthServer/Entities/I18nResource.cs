using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 다국어 자원 정보를 관리하는 엔티티
/// </summary>
[Table("i18n_resources", Schema = "scom")]
public class I18nResource : BaseEntity
{
    /// <summary>다국어 키 (예: common.expandAll)</summary>
    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    /// <summary>로케일 (예: ko, en-US)</summary>
    [Required]
    [MaxLength(20)]
    public string Locale { get; set; } = string.Empty;

    /// <summary>다국어 번역 값</summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>카테고리/모듈 (예: common, ui, system)</summary>
    [MaxLength(100)]
    public string? Category { get; set; }
}
