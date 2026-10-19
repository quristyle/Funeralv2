using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>사람 하나가 지나온 자리 한 점. 잴 때마다 한 행이고, 설정 표의 좌표는 덮어쓰는 것과 달리 이 표는 쌓는다.
/// 브라우저가 방금 잰 좌표일 때만 쌓는다. 위치를 허용하지 않은 사람은 행이 없다.</summary>
[Table("location_tracks", Schema = "scom")]
public class LocationTrack : BaseEntity<string>
{
    public LocationTrack()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>주인의 종류. 포털 계정은 <c>jsini</c> 다.</summary>
    [Required]
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 포털이면 로그인 아이디다.</summary>
    [Required]
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>브라우저 Geolocation 이 준 위도(10진 도).</summary>
    [Column("lat")]
    public double Lat { get; set; }

    /// <summary>브라우저 Geolocation 이 준 경도(10진 도).</summary>
    [Column("lon")]
    public double Lon { get; set; }

    /// <summary>브라우저가 말한 오차 반지름(m). 모르는 경우가 흔하다 — 좌표만 싣는 길에는 이 칸이 없다.</summary>
    [Column("accuracy")]
    public double? Accuracy { get; set; }

    /// <summary>그때 알아낸 지역 이름. 자리가 안 바뀌면 기상청을 부르지 않으므로 비는 줄이 흔하다.</summary>
    [Column("place")]
    public string? Place { get; set; }

    /// <summary>직전 좌표에서 자리가 바뀐 점인가(300m — GeoLocator.MoveThresholdMeters). 묶음이 맞는지 견줄 단서로 남긴다.</summary>
    [Column("moved")]
    public bool Moved { get; set; }

    /// <summary>잰 때. <b>UTC</b> 다.</summary>
    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
