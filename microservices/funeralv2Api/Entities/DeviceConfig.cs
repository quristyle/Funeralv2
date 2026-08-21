using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비 기본 설정 엔티티 클래스 (Device 1:1 관계)
/// 음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.
/// </summary>
[Table("device_configs", Schema = "smfr")]
public class DeviceConfig : BaseEntity<string>
{
    /// <summary>
    /// DeviceConfig 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public DeviceConfig()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 장비 식별자 (ID)
    /// </summary>
    [Required]
    [Column("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 장비 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }

    /// <summary>
    /// 기기 음량 (0-100, 기본값: 50)
    /// </summary>
    [Column("volume")]
    public int Volume { get; set; } = 50;

    /// <summary>
    /// 화면 밝기 (0-100, 기본값: 80)
    /// </summary>
    [Column("brightness")]
    public int Brightness { get; set; } = 80;

    /// <summary>
    /// 일일 자동 재시작 시각 (예: HH:mm 형식)
    /// </summary>
    [Column("reboot_time")]
    [MaxLength(5)]
    public string? RebootTime { get; set; }

    /// <summary>
    /// 자동 전원 제어 사용 여부
    /// </summary>
    [Column("is_auto_power")]
    public bool IsAutoPower { get; set; } = false;

    /// <summary>
    /// 자동 켜짐 시각 (예: HH:mm 형식)
    /// </summary>
    [Column("power_on_time")]
    [MaxLength(5)]
    public string? PowerOnTime { get; set; }

    /// <summary>
    /// 자동 꺼짐 시각 (예: HH:mm 형식)
    /// </summary>
    [Column("power_off_time")]
    [MaxLength(5)]
    public string? PowerOffTime { get; set; }
}
