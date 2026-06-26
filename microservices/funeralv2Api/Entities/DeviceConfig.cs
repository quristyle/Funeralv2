using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비 기본 설정 (Device 1:1 관계)
/// 음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.
/// </summary>
[Table("device_configs", Schema = "smfr")]
public class DeviceConfig : BaseEntity<string>
{
    public DeviceConfig()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>장비 FK</summary>
    [Required]
    [Column("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }

    /// <summary>기기 음량 (0-100)</summary>
    [Column("volume")]
    public int Volume { get; set; } = 50;

    /// <summary>화면 밝기 (0-100)</summary>
    [Column("brightness")]
    public int Brightness { get; set; } = 80;

    /// <summary>일일 자동 재시작 시각 (HH:mm)</summary>
    [Column("reboot_time")]
    [MaxLength(5)]
    public string? RebootTime { get; set; }

    /// <summary>자동 전원 제어 사용 여부</summary>
    [Column("is_auto_power")]
    public bool IsAutoPower { get; set; } = false;

    /// <summary>자동 켜짐 시각 (HH:mm)</summary>
    [Column("power_on_time")]
    [MaxLength(5)]
    public string? PowerOnTime { get; set; }

    /// <summary>자동 꺼짐 시각 (HH:mm)</summary>
    [Column("power_off_time")]
    [MaxLength(5)]
    public string? PowerOffTime { get; set; }
}
