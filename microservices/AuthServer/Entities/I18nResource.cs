using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 다국어 리소스 정보를 관리하는 엔티티 클래스
/// </summary>
[Table("i18n_resources", Schema = "scom")]
public class I18nResource : BaseEntity
{
    /// <summary>
    /// 다국어 리소스 키 (예: common.expandAll)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 로케일 설정 (예: ko, en-US)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// 다국어 번역 결과 텍스트 값
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 카테고리 또는 모듈 구분 (예: common, ui, system)
    /// </summary>
    [MaxLength(100)]
    public string? Category { get; set; }
}
