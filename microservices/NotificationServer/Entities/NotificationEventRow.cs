using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>알림 이벤트 하나. 「어떤 일이 일어났을 때 보내는 알림인가」를 코드가 아니라 표에 둔다. 식별자가 곧 이벤트 코드다. Governed 가 거짓이면 목록에만 보이고 정책은 못 건다.</summary>
[Table("notification_events", Schema = "scom")]
public class NotificationEventRow : BaseEntity<string>
{
    /// <summary>사람이 읽는 이름. 「배포 완료」·「쪽지 도착」.</summary>
    [Required]
    [MaxLength(128)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>언제 나가는 알림인지 한 줄. 화면이 그대로 보여 준다.</summary>
    [MaxLength(512)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>알림구분(NOTI_CATEGORY) 코드값. 메일로만 나가 알림함에 안 쌓이는 것은 비어 있을 수 있다.</summary>
    [MaxLength(64)]
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>어느 서비스가 보내나. 화면의 「발생처」 칸이고 조회에는 안 쓴다.</summary>
    [MaxLength(64)]
    [Column("source")]
    public string? Source { get; set; }

    /// <summary>대상 성격. ROLE 이면 정책의 역할이 받는 사람 목록이고, USER 면 받는 사람은 이미 정해져 있고 정책은 거름막이다.</summary>
    [Required]
    [MaxLength(16)]
    [Column("target_kind")]
    public string TargetKind { get; set; } = NotificationTargetKinds.Role;

    /// <summary>앱 푸시로 나갈 수 있나. 거짓이면 화면이 그 체크 칸을 안 그린다.</summary>
    [Column("supports_push")]
    public bool SupportsPush { get; set; } = true;

    /// <summary>이메일로 나갈 수 있나.</summary>
    [Column("supports_email")]
    public bool SupportsEmail { get; set; }

    /// <summary>이 이벤트의 메일을 푸시 경로가 함께 낸다. 헬프데스크 LEGACY 는 제 관리자 표로 메일을 이미 보내므로 두 목록에 든 사람은 두 통을 받는다.</summary>
    [Column("email_from_push")]
    public bool EmailFromPush { get; set; }

    /// <summary>이 이벤트에 정책이 실제로 걸리나. 거짓이면 설정은 받아 두되 발송은 지금 그대로다.</summary>
    [Column("governed")]
    public bool Governed { get; set; } = true;

    /// <summary>받는 사람의 정본이 이 표인가. 참이면 역할을 하나도 안 걸었을 때 아무에게도 안 간다(제한 없음이 아니다). 소개 사이트 문의가 그렇다.</summary>
    [Column("policy_required")]
    public bool PolicyRequired { get; set; }

    /// <summary>쓰나. 끄면 정책 줄 유무와 관계없이 아무에게도 안 간다. 역할이 비어 있는 것(지금 그대로 나간다)과 다르다.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>목록 차례. 구분 묶음이 흩어지지 않게 10 단위로 매긴다.</summary>
    [Column("order_no")]
    public int OrderNo { get; set; }
}

/// <summary><see cref="NotificationEventRow.TargetKind"/> 에 들어가는 값.</summary>
public static class NotificationTargetKinds
{
    /// <summary>역할로 간다. 정책의 역할이 곧 받는 사람이다.</summary>
    public const string Role = "ROLE";

    /// <summary>당사자에게 간다. 정책의 역할은 거름막이다.</summary>
    public const string User = "USER";
}
