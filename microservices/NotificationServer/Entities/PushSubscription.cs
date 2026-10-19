using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>Web Push 구독 한 건(브라우저 하나 = 행 하나). 주인은 (ownerType, ownerKey) 한 쌍이다. 팀·회사는 모르고, 누구에게 보낼지는 부르는 쪽이 정한다.</summary>
[Table("push_subscriptions", Schema = "scom")]
public class PushSubscription : BaseEntity<string>
{
    public PushSubscription()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>푸시 서비스가 준 발송 주소. 브라우저·기기마다 다르고 실질적인 신원이다. 같은 브라우저가 다시 구독하면 같은 값이 오므로 새로 만들지 않고 갱신한다.</summary>
    [Required]
    [Column("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>암호화 키 (브라우저가 준 값)</summary>
    [Required]
    [Column("p256dh")]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>인증 비밀 (브라우저가 준 값)</summary>
    [Required]
    [Column("auth")]
    public string Auth { get; set; } = string.Empty;

    /// <summary>주인의 종류. 예: <c>jsini</c> · <c>helpdesk-admin</c> · <c>helpdesk-customer</c></summary>
    [Required]
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 종류에 따라 로그인 아이디이거나 숫자 문자열이다.</summary>
    [Required]
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>어느 시스템에서 구독했나 (참고용). 예: <c>portal</c> · <c>helpdesk</c></summary>
    [Column("source")]
    public string? Source { get; set; }

    /// <summary>구독을 만든 브라우저 정보 (참고용). 문제를 쫓을 때 쓴다.</summary>
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("device_type")]
    public string? DeviceType { get; set; }
    [Column("platform")]
    public string? Platform { get; set; }
    [Column("platform_version")]
    public string? PlatformVersion { get; set; }
    [Column("device_vendor")]
    public string? DeviceVendor { get; set; }
    [Column("device_model")]
    public string? DeviceModel { get; set; }
    [Column("browser")]
    public string? Browser { get; set; }
    [Column("browser_version")]
    public string? BrowserVersion { get; set; }
    [Column("browser_engine")]
    public string? BrowserEngine { get; set; }
    [Column("is_mobile")]
    public bool? IsMobile { get; set; }
    [Column("is_standalone")]
    public bool? IsStandalone { get; set; }
    [Column("display_mode")]
    public string? DisplayMode { get; set; }
    [Column("screen_width")]
    public int? ScreenWidth { get; set; }
    [Column("screen_height")]
    public int? ScreenHeight { get; set; }
    [Column("viewport_width")]
    public int? ViewportWidth { get; set; }
    [Column("viewport_height")]
    public int? ViewportHeight { get; set; }
    [Column("device_pixel_ratio")]
    public double? DevicePixelRatio { get; set; }
    [Column("color_depth")]
    public int? ColorDepth { get; set; }
    [Column("hardware_concurrency")]
    public int? HardwareConcurrency { get; set; }
    [Column("device_memory_gb")]
    public double? DeviceMemoryGb { get; set; }
    [Column("max_touch_points")]
    public int? MaxTouchPoints { get; set; }
    [Column("language")]
    public string? Language { get; set; }
    [Column("languages")]
    public string? Languages { get; set; }
    [Column("time_zone")]
    public string? TimeZone { get; set; }
    [Column("connection_type")]
    public string? ConnectionType { get; set; }
    [Column("effective_connection_type")]
    public string? EffectiveConnectionType { get; set; }
    [Column("user_agent_data_json")]
    public string? UserAgentDataJson { get; set; }

    /// <summary>마지막으로 발송에 성공한 시각.</summary>
    [Column("last_sent_at")]
    public DateTime? LastSentAt { get; set; }

    /// <summary>연달아 실패한 횟수. 404/410 이면 그 구독은 죽은 것이라 즉시 지우고, 그 밖의 실패는 세어 둔다.</summary>
    [Column("failure_count")]
    public int FailureCount { get; set; }
}
