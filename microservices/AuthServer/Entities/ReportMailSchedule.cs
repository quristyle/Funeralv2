using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 보고서 메일 배치 하나 — <b>어떤 보고서를</b> · <b>어느 역할에게</b> ·
/// <b>얼마나 자주</b> 보낼지를 묶은 줄.
/// </summary>
/// <remarks>
/// <para>
/// [왜 AuthServer 인가]
/// </para>
///
/// <para>
/// 이 배치가 가리키는 것 둘이 모두 <c>scom</c> 에 있다 — 받는 쪽은
/// 역할(<c>scom.roles</c>)이고, 보낼 보고서의 목록은 메뉴
/// (<c>scom.system_menus</c>)다. 다른 서비스에 두면 역할·메뉴를 둘이 알게 되고
/// 한쪽만 고쳐진다(<see cref="MenuUsageLog"/> 머리말과 같은 판단이다).
/// </para>
///
/// <para>
/// [시각은 한국 벽시계로 적는다 — 이 표만 그렇다]
/// </para>
///
/// <para>
/// 이 시스템의 시각은 전부 UTC 다(<c>docs/utc-time.md</c>). 그런데
/// <see cref="SendHourKst"/> 는 「순간」이 아니라 <b>사람이 고른 벽시계 시각</b>
/// 이다 — 「아침 여덟 시에 받겠다」는 뜻이지 「23:00 UTC 에 받겠다」가 아니다.
/// UTC 로 적어 두면 지금은 같은 값이지만 그 뜻이 사라져, 설정 화면이 다시
/// 아홉 시간을 빼서 보여 줘야 하고 거기서 갈린다. 그래서 <b>시·분만</b> 한국
/// 벽시계로 두고, 실제로 보낸 순간(<see cref="LastSentAt"/>)은 UTC 다.
/// 그 경계는 <c>docs/utc-time.md</c> 의 표에 한 줄 적어 두었다.
/// </para>
///
/// <para>
/// [보고서를 메뉴 열쇠로 가리킨다]
/// </para>
///
/// <para>
/// 고른 보고서는 <c>route_key</c>(예: <c>helpdesk.report.weekly</c>)로 적는다 —
/// 메뉴 식별자가 아니라 열쇠인 까닭은 <c>web/CLAUDE.md</c> 「연결 고리는 URL 이
/// 아니라 열쇠다」와 같다. 메일에 싣는 링크도 그 열쇠로 푼다.
/// </para>
/// </remarks>
[Table("report_mail_schedules", Schema = "scom")]
public class ReportMailSchedule : BaseEntity<string>
{
    /// <summary>배치 이름. 목록에서 사람이 알아보는 글자다</summary>
    [Required]
    [MaxLength(128)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 주기. <c>ReportMailFrequency</c> 의 값 하나 — <c>DAILY</c> · <c>WEEKDAY</c> ·
    /// <c>WEEKLY</c> · <c>MONTHLY</c>.
    /// </summary>
    [Required]
    [MaxLength(16)]
    [Column("frequency")]
    public string Frequency { get; set; } = ReportMailFrequency.Weekly;

    /// <summary>
    /// 주간일 때 보낼 요일. 1(월) ~ 7(일) — ISO 순서다.
    /// 다른 주기에서는 비어 있다.
    /// </summary>
    [Column("day_of_week")]
    public int? DayOfWeek { get; set; }

    /// <summary>
    /// 월간일 때 보낼 날. 1 ~ 31.
    /// <b>그 달에 없는 날이면 말일에 보낸다</b>(31 일을 고른 2월은 28·29일).
    /// </summary>
    [Column("day_of_month")]
    public int? DayOfMonth { get; set; }

    /// <summary>보낼 시각(시). <b>한국 벽시계</b>다 — 머리말 참고</summary>
    [Column("send_hour_kst")]
    public int SendHourKst { get; set; } = 8;

    /// <summary>보낼 시각(분). <b>한국 벽시계</b>다</summary>
    [Column("send_minute_kst")]
    public int SendMinuteKst { get; set; }

    /// <summary>켜져 있나. 꺼 두면 발송기가 건너뛴다</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>메일 머리에 붙일 한마디. 비워도 된다</summary>
    [MaxLength(512)]
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>마지막으로 보낸 때 (UTC). 한 번도 안 보냈으면 비어 있다</summary>
    [Column("last_sent_at")]
    public DateTime? LastSentAt { get; set; }

    /// <summary>
    /// 마지막 발송의 결과. 성공이면 받은 사람 수가, 실패면 까닭이 적힌다.
    /// </summary>
    /// <remarks>
    /// <b>화면이 볼 수 있어야 한다.</b> 서버 로그에만 적으면 「메일이 왜 안
    /// 오지」를 묻는 사람이 닿을 수 없는 자리에 답이 있게 된다.
    /// </remarks>
    [MaxLength(512)]
    [Column("last_result")]
    public string? LastResult { get; set; }

    /// <summary>고른 보고서들</summary>
    public ICollection<ReportMailScheduleReport>? Reports { get; set; }

    /// <summary>받을 역할들</summary>
    public ICollection<ReportMailScheduleRole>? Roles { get; set; }
}

/// <summary>
/// 배치가 고른 보고서 하나. 가리키는 값은 메뉴 열쇠(<c>route_key</c>)다.
/// </summary>
/// <remarks>
/// <b>외래키를 메뉴에 걸지 않는다.</b> 메뉴를 지웠다고 배치가 함께 사라지면
/// 안 되고(그때는 화면이 「없어진 보고서」라고 말해 주는 편이 낫다), 열쇠는
/// 메뉴 표의 기본키도 아니다.
/// </remarks>
[Table("report_mail_schedule_reports", Schema = "scom")]
public class ReportMailScheduleReport : BaseEntity<int>
{
    /// <summary>어느 배치의 것인가</summary>
    [Required]
    [MaxLength(64)]
    [Column("schedule_id")]
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>배치 탐색 속성</summary>
    [ForeignKey(nameof(ScheduleId))]
    public virtual ReportMailSchedule? Schedule { get; set; }

    /// <summary>보고서 화면의 열쇠 (예: <c>helpdesk.report.weekly</c>)</summary>
    [Required]
    [MaxLength(128)]
    [Column("report_key")]
    public string ReportKey { get; set; } = string.Empty;
}

/// <summary>
/// 배치를 받을 역할 하나.
/// </summary>
/// <remarks>
/// <b>사람이 아니라 역할을 적는다.</b> 담당자가 바뀔 때 고칠 곳이 역할 하나로
/// 끝나야 하기 때문이고, 그것이 이 화면을 만든 까닭 그 자체다
/// (<c>AccountMailClient.SendToRoleAsync</c> 머리말과 같은 선이다).
/// </remarks>
[Table("report_mail_schedule_roles", Schema = "scom")]
public class ReportMailScheduleRole : BaseEntity<int>
{
    /// <summary>어느 배치의 것인가</summary>
    [Required]
    [MaxLength(64)]
    [Column("schedule_id")]
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>배치 탐색 속성</summary>
    [ForeignKey(nameof(ScheduleId))]
    public virtual ReportMailSchedule? Schedule { get; set; }

    /// <summary>역할 식별자 (<c>scom.roles.id</c>)</summary>
    [Required]
    [MaxLength(64)]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>역할 탐색 속성</summary>
    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }
}

/// <summary>
/// 주기의 값들. <b>글자로 저장한다</b> — 숫자로 두면 DB 를 직접 들여다볼 때
/// 무엇인지 알 수 없고, 값이 늘 때 번호를 맞춰 주어야 한다.
/// </summary>
public static class ReportMailFrequency
{
    /// <summary>날마다</summary>
    public const string Daily = "DAILY";

    /// <summary>주중(월~금)만</summary>
    public const string Weekday = "WEEKDAY";

    /// <summary>주마다 — 요일을 고른다</summary>
    public const string Weekly = "WEEKLY";

    /// <summary>달마다 — 날을 고른다</summary>
    public const string Monthly = "MONTHLY";

    /// <summary>아는 값인가</summary>
    public static bool IsKnown(string? value) =>
        value is Daily or Weekday or Weekly or Monthly;
}
