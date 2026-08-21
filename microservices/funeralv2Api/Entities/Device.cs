using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비(디바이스) 정보 엔티티 클래스
/// </summary>
[Table("devices", Schema = "smfr")]
public class Device : BaseEntity<string>
{
    /// <summary>
    /// Device 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Device()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 장비명
    /// </summary>
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 장비 약칭
    /// </summary>
    [Column("short_name")]
    [MaxLength(100)]
    public string? ShortName { get; set; }

    /// <summary>
    /// 장비 코드 (고유 식별 코드)
    /// </summary>
    [Required]
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 장비 유형 (예: DID, KIOSK, SIGNBOARD 등, 기본값: DID)
    /// </summary>
    [Column("device_type")]
    [MaxLength(50)]
    public string DeviceType { get; set; } = "DID";

    /// <summary>
    /// IP 주소
    /// </summary>
    [Column("ip_address")]
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// MAC 주소
    /// </summary>
    [Column("mac_address")]
    [MaxLength(50)]
    public string? MacAddress { get; set; }

    /// <summary>
    /// 공인 IP 주소
    /// </summary>
    [Column("public_ip_address")]
    [MaxLength(50)]
    public string? PublicIpAddress { get; set; }

    /// <summary>
    /// 장비 상태 (예: ONLINE, OFFLINE, UNKNOWN 등, 기본값: UNKNOWN)
    /// </summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "UNKNOWN";

    /// <summary>
    /// 마지막 상태 확인 시간
    /// </summary>
    [Column("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 배정된 호실(빈소) 식별자 (ID)
    /// </summary>
    [Column("room_id")]
    public string? RoomId { get; set; }

    /// <summary>
    /// 배정된 호실 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(RoomId))]
    public Room? Room { get; set; }

    /// <summary>
    /// 배정된 층 식별자 (ID)
    /// </summary>
    [Column("floor_id")]
    public string? FloorId { get; set; }

    /// <summary>
    /// 배정된 층 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(FloorId))]
    public Floor? Floor { get; set; }

    /// <summary>
    /// 배정된 건물 식별자 (ID)
    /// </summary>
    [Column("building_id")]
    public string? BuildingId { get; set; }

    /// <summary>
    /// 배정된 건물 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }
    
    /// <summary>
    /// 소속 회사 식별자 (ID)
    /// </summary>
    [Column("company_id")]
    public string? CompanyId { get; set; }
}
