using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비 정보
/// </summary>
[Table("devices", Schema = "smfr")]
public class Device : BaseEntity<string>
{
    public Device()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("code")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Column("device_type")]
    [MaxLength(50)]
    public string DeviceType { get; set; } = "DID"; // DID, KIOSK, SIGNBOARD 등

    [Column("ip_address")]
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [Column("mac_address")]
    [MaxLength(50)]
    public string? MacAddress { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "UNKNOWN"; // ONLINE, OFFLINE, UNKNOWN

    [Column("room_id")]
    public string? RoomId { get; set; }
    [ForeignKey(nameof(RoomId))]
    public Room? Room { get; set; }

    [Column("floor_id")]
    public string? FloorId { get; set; }
    [ForeignKey(nameof(FloorId))]
    public Floor? Floor { get; set; }

    [Column("building_id")]
    public string? BuildingId { get; set; }
    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }
    
    // Building, Floor, Room으로부터 CompanyId를 추상화하기 위한 속성
    [NotMapped]
    public string? CompanyId { get; set; }
}
