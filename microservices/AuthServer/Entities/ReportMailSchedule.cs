using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 보고서 메일 배치 하나 — <b>어떤 보고서를</b> · <b>어느 역할에게</b> ·
/// <b>얼마나 자주</b> 보낼지를 묶은 줄.
/// </summary>
/// 배치는 scom 에 있는 역할·메뉴를 가리키므로 AuthServer 에 둔다 — 다른 서비스에 두면 둘이 알게 되어 한쪽만 고쳐진다.
/// SendHourKst 만 한국 벽시계로 둔다(사람이 고른 시각이라 UTC 로 적으면 뜻이 사라진다). 보낸 순간 LastSentAt 은 UTC 다. 고른 보고서는 메뉴 열쇠(route_key)로 가리킨다.
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
    /// 화면이 볼 수 있어야 한다 — 서버 로그에만 적으면 메일이 왜 안 오는지 묻는 사람이 닿을 수 없다.
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
/// 외래키를 메뉴에 걸지 않는다 — 메뉴를 지웠다고 배치가 같이 사라지면 안 되고, 열쇠는 메뉴 표의 기본키도 아니다.
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
/// 사람이 아니라 역할을 적는다 — 담당자가 바뀔 때 고칠 곳이 역할 하나로 끝나야 하기 때문이다.
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
