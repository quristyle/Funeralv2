using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 계정별 장례식장 업무 설정 한 줄. 옛 t_account_conf 에 해당하며 코드 이름·기본값은 SettingCatalog 가 든다.
/// 옛 코드 중 vben 개인 환경설정과 겹치는 넷은 옮기지 않았다.
/// </summary>
[Table("account_settings")]
public class AccountSetting
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>설정 주인 (게이트웨이가 붙여 준 X-User-Id)</summary>
    [Required]
    [Column("user_id")]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>설정 코드 (옛 <c>conf_cd</c>)</summary>
    [Required]
    [Column("setting_code")]
    [MaxLength(100)]
    public string SettingCode { get; set; } = string.Empty;

    /// <summary>설정 값. 켬/끔은 <c>"Y"</c>/<c>"N"</c> 으로 적는다 — 옛 표기를 그대로 쓴다.</summary>
    [Column("setting_value")]
    [MaxLength(500)]
    public string? SettingValue { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
