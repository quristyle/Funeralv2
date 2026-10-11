using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// 알림 <b>이벤트</b> 하나 — 「어떤 일이 일어났을 때 보내는 알림인가」.
/// </summary>
/// <remarks>
/// <para>
/// [왜 표로 두나 — 코드에 적어 두면 아홉 번째가 생기는 날 아무도 안 고친다]
/// </para>
///
/// <para>
/// 알림이 나가는 자리를 저장소 전체에서 세어 열셋을 찾았고, 그것이 처음 줄들이다
/// (<c>deploy/sql/notify-policy-2026-10-11.sql</c>). 코드에 박아 두면 설정 화면이
/// 코드 배포를 기다려야 하고, 보고서 메일이 보고서 목록을 메뉴에서 읽는 것과
/// 같은 까닭으로 표에 둔다(<c>docs/report-mail.md</c>).
/// </para>
///
/// <para>
/// [식별자가 곧 이벤트 코드다]
/// </para>
///
/// <para>
/// GUID 를 쓰지 않는다. 보내는 쪽이 적어 보내는 글자가
/// <see cref="JSini.Shared.DTOs.NotificationEvents"/> 의 상수이고, 그 글자로 바로
/// 찾을 수 있어야 발송 경로에 조회가 하나 더 안 붙는다.
/// </para>
///
/// <para>
/// [<see cref="Governed"/> 가 거짓인 줄이 있다 — 거짓말을 하지 않으려고]
/// </para>
///
/// <para>
/// 헬프데스크의 접수·완료·종료 알림은 <b>알림 서버를 거치지 않는다</b>
/// (<c>HelpDeskServer/Utilities/PushUtil</c> · <c>EMailUtil</c> 이 직접 보낸다).
/// 그 줄을 목록에서 빼면 관리자가 「그 알림은 없다」고 읽고, 정책을 걸 수 있는
/// 것처럼 보이게 두면 껐는데도 계속 온다. 그래서 <b>보이되 못 거는</b> 상태를
/// 표에 둔다 — 화면이 그 줄에 「정책 적용 안 됨」을 적어 준다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 알림구분(<c>NOTI_CATEGORY</c>) 코드값. 비어 있을 수 있다 — 보고서 메일처럼
    /// 메일로만 나가 알림함에 쌓이지 않는 것은 붙일 갈래가 없다.
    /// </summary>
    [MaxLength(64)]
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>어느 서비스가 보내나. 화면의 「발생처」 칸이고 조회에는 안 쓴다.</summary>
    [MaxLength(64)]
    [Column("source")]
    public string? Source { get; set; }

    /// <summary>
    /// 대상 성격. <c>ROLE</c> 이면 역할로 가고 <c>USER</c> 면 당사자에게 간다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>정책이 하는 일이 이 값에 따라 갈린다.</b> <c>ROLE</c> 인 이벤트는
    /// 정책에 적힌 역할이 곧 <b>받는 사람 목록</b>이고(더하기), <c>USER</c> 인
    /// 이벤트는 받는 사람이 이미 정해져 있으므로 정책은 <b>거름막</b>으로만
    /// 쓴다(그 역할을 하나도 안 가진 사람은 뺀다).
    /// </para>
    /// <para>
    /// 가르지 않으면 둘 중 하나가 반드시 틀린다 — 쪽지 알림에 역할을 더하면
    /// 남의 쪽지가 관리자에게 가고, 배포 알림을 거르기만 하면 정책을 적어도
    /// 받는 사람이 늘지 않는다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 이 이벤트의 메일을 <b>푸시 경로가 함께 내나</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [메일이 나가는 길이 두 갈래라 가려야 한다]
    /// </para>
    ///
    /// <para>
    /// 어떤 이벤트는 부르는 쪽이 <c>/emails/send</c> 를 <b>따로</b> 부른다
    /// (가입 신청 · AI 작업 · 소개 사이트 문의 · 보고서 메일). 그 길은 이미
    /// 정책을 묻고 있으므로 화면의 「이메일」 체크가 그대로 듣는다.
    /// </para>
    ///
    /// <para>
    /// 나머지는 <b>푸시만</b> 보낸다(배포 완료 · 헬프데스크 요청 · 생일 · 기상 ·
    /// 새 기기 구독). 그쪽에서 체크를 켜 봐야 메일을 낼 사람이 아무도 없어서,
    /// 한동안 <see cref="SupportsEmail"/> 을 거짓으로 두어 <b>체크 칸을 잠가</b>
    /// 두었다 — 「켰는데 왜 안 오지」를 만들지 않으려고. 이 값이 참이면
    /// <c>PushSender</c> 가 같은 내용을 메일로도 낸다.
    /// </para>
    ///
    /// <para>
    /// <b>앞 갈래와 겹치게 만들지 않는다.</b> 부르는 쪽이 메일을 따로 내는데
    /// 여기서도 내면 같은 알림이 두 통 간다.
    /// </para>
    ///
    /// <para>
    /// [<c>HELPDESK</c> 하나는 이미 겹친다 — 알고 켠 것이다]
    /// </para>
    ///
    /// <para>
    /// 헬프데스크는 새 요청이 올라오면 <b>제 관리자 목록</b>으로 메일을 이미
    /// 보낸다(<c>HelpDeskServer</c> 의
    /// <c>AdminService.GetAdminEmailsForNotificationAsync</c> → <c>EMailUtil</c> —
    /// <c>helpdesk</c> DB 의 <c>receiveEmail</c> 을 켠 사람들). 그 길이
    /// <c>HELPDESK_LEGACY</c> 다.
    /// </para>
    ///
    /// <para>
    /// 받는 사람 목록이 <b>다르다</b> — 저쪽은 헬프데스크의 관리자 표이고
    /// 이쪽은 포털의 역할표다. 그래서 「두 통 가니까 켜지 마라」가 아니라
    /// <b>두 목록에 다 든 사람은 두 통을 받는다</b>가 맞는 말이고, 이벤트
    /// 설명에 그 한 줄을 적어 두었다. 겹침이 사라지는 때는
    /// <c>HELPDESK_LEGACY</c> 를 알림 서버로 옮길 때다
    /// (<c>docs/notify-policy.md</c>).
    /// </para>
    ///
    /// <para>
    /// [켜기 전에 봐야 할 것 — <b>갈래별 스위치가 있는 이벤트</b>]
    /// </para>
    ///
    /// <para>
    /// 곁가지는 <b>전체 메일 스위치</b>(<c>email_enabled</c>)만 본다. 그 아래
    /// 갈래 스위치는 직발송 쪽에만 있어서(<c>HELPDESK_COMMENT</c> 의
    /// <c>comment_email_enabled</c> —
    /// <c>NotificationPreferenceService.GetCommentEmailDisabledLoginIdsAsync</c>),
    /// 그런 이벤트에 이 값을 켜면 <b>본인이 꺼 둔 갈래 스위치를 우회한다.</b>
    /// 켜려거든 곁가지에도 그 판정을 함께 옮긴다.
    /// </para>
    /// </remarks>
    [Column("email_from_push")]
    public bool EmailFromPush { get; set; }

    /// <summary>
    /// 이 이벤트에 정책이 <b>실제로 걸리나</b>. 거짓이면 설정은 받아 두되 발송은
    /// 지금 그대로다 — 머리말의 「거짓말을 하지 않으려고」 참고.
    /// </summary>
    [Column("governed")]
    public bool Governed { get; set; } = true;

    /// <summary>
    /// 쓰나. <b>끄면 이 이벤트의 알림이 아무에게도 안 간다</b>(정책 줄이 있든
    /// 없든).
    /// </summary>
    /// <remarks>
    /// 역할을 하나도 안 고른 것과 뜻이 다르다. 역할이 비면 「아직 안 정했다」라
    /// 지금 그대로 나가고, 이것을 끄는 것은 「보내지 마라」다. 화면이 끌 때
    /// 한 번 묻는다.
    /// </remarks>
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
