using AuthServer.Entities;
using JSini.Shared.Infrastructure.Time;

namespace AuthServer.Services;

/// <summary>
/// 주기 하나를 읽어 <b>언제 보내야 하는가</b>를 셈한다.
/// </summary>
/// <remarks>
/// <para>
/// 발송기(<see cref="ReportMailDispatcher"/>)와 화면에 보여 줄 「다음 발송」이
/// <b>같은 규칙</b>을 써야 해서 따로 떼어 두었다. 두 곳에 적으면 화면이
/// 「내일 08:00」이라 말하는데 실제로는 안 나가는 일이 생기고, 그런 숫자는
/// 한 번 어긋나면 아무도 안 믿는다.
/// </para>
///
/// <para>
/// [셈은 한국 벽시계로 한다]
/// </para>
///
/// <para>
/// 사람이 고른 것이 「아침 여덟 시」라서다. UTC 로 셈하면 「매달 1일」이
/// 한국의 1일 오전 아홉 시 전에는 지난달로 읽힌다 — <c>docs/utc-time.md</c> 가
/// 「달력으로 물어야 답이 되는 자리」라 부르는 것이 이것이다.
/// <b>주고받는 값(<c>NextRunAt</c>)은 UTC 로 되돌려 준다.</b>
/// </para>
///
/// <para>
/// [상태를 안 들고 있다]
/// </para>
///
/// <para>
/// 전부 정적 함수이고 「지금」을 인자로 받는다. 시계를 안에서 읽으면 어느
/// 경계값에서 틀리는지 확인할 길이 없다.
/// </para>
/// </remarks>
public static class ReportMailSchedulePlan
{
    /// <summary>
    /// 고른 시각이 지난 뒤 <b>이만큼까지만</b> 보낸다.
    /// </summary>
    /// <remarks>
    /// 서비스가 몇 시간 내려가 있다가 올라왔을 때 지나간 칸을 뒤늦게 보내면
    /// <b>밤 열한 시에 아침 보고서</b>가 온다. 날짜가 걸린 자료라 늦은 것은
    /// 안 보내는 편이 맞다(<c>LocalWeatherNotifyService</c> 와 같은 선이다).
    /// 다만 날씨보다 느슨하게 둔다 — 보고서는 한두 시간 늦어도 읽을 값이 있다.
    /// </remarks>
    public static readonly TimeSpan Grace = TimeSpan.FromHours(3);

    /// <summary>
    /// 지금 보낼 때가 되었나. <paramref name="utcNow"/> 는 UTC 로 준다.
    /// </summary>
    /// <remarks>
    /// 판정이 셋이다 — <b>오늘이 보내는 날인가</b> · <b>그 시각이 지났는가</b> ·
    /// <b>그 칸에 이미 보냈는가</b>. 마지막 것을 메모리에 기억해 두면 서비스를
    /// 다시 띄울 때마다 또 간다. 그래서 표에 적어 둔 <c>LastSentAt</c> 으로 본다.
    /// </remarks>
    public static bool IsDue(ReportMailSchedule schedule, DateTime utcNow)
    {
        if (!schedule.IsActive || schedule.IsDeleted) return false;

        var nowKst = AppTime.ToKorea(utcNow);
        var slot = SlotOn(schedule, DateOnly.FromDateTime(nowKst));

        if (slot is null) return false;
        if (nowKst < slot.Value) return false;
        if (nowKst - slot.Value > Grace) return false;

        // 이미 이 칸에 보냈나. 보낸 순간은 UTC 라 한국 벽시계로 옮겨 견준다.
        if (schedule.LastSentAt is { } sent && AppTime.ToKorea(sent) >= slot.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 다음에 보낼 때 (UTC). 꺼져 있거나 주기를 모르면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// 오늘의 칸이 아직 안 지났고 그 칸에 안 보냈으면 <b>오늘</b>이 답이다.
    /// 그 뒤로는 하루씩 더하며 보내는 날을 찾는다 — 달마다·주마다의 규칙이
    /// 서로 달라 식으로 한 번에 구하는 것보다 틀릴 자리가 적다.
    /// 1년을 돌아도 못 찾으면(있을 수 없는 설정) 포기한다.
    /// </remarks>
    public static DateTime? NextRunUtc(ReportMailSchedule schedule, DateTime utcNow)
    {
        if (!schedule.IsActive || schedule.IsDeleted) return null;
        if (!ReportMailFrequency.IsKnown(schedule.Frequency)) return null;

        var nowKst = AppTime.ToKorea(utcNow);
        var sentKst = schedule.LastSentAt is { } sent ? AppTime.ToKorea(sent) : (DateTime?)null;

        var day = DateOnly.FromDateTime(nowKst);

        for (var i = 0; i < 400; i++, day = day.AddDays(1))
        {
            if (SlotOn(schedule, day) is not { } slot) continue;

            // 지나간 칸은 건너뛴다. 오늘 칸이라도 이미 보냈으면 다음이다.
            if (slot <= nowKst && (sentKst is null || sentKst < slot))
            {
                // 아직 유예 안이면 곧 나간다 — 그때는 「지금」이 답이다.
                if (nowKst - slot <= Grace) return AppTime.FromKorea(slot);
                continue;
            }

            if (slot <= nowKst) continue;

            return AppTime.FromKorea(slot);
        }

        return null;
    }

    /// <summary>
    /// 그 날짜에 보내는 칸이 있으면 그 <b>한국 벽시계</b> 시각, 없으면 <c>null</c>.
    /// </summary>
    private static DateTime? SlotOn(ReportMailSchedule schedule, DateOnly day)
    {
        if (!SendsOn(schedule, day)) return null;

        var hour = Math.Clamp(schedule.SendHourKst, 0, 23);
        var minute = Math.Clamp(schedule.SendMinuteKst, 0, 59);

        return day.ToDateTime(new TimeOnly(hour, minute));
    }

    /// <summary>그 날짜가 보내는 날인가.</summary>
    private static bool SendsOn(ReportMailSchedule schedule, DateOnly day) => schedule.Frequency switch
    {
        ReportMailFrequency.Daily => true,

        ReportMailFrequency.Weekday =>
            day.DayOfWeek is not (System.DayOfWeek.Saturday or System.DayOfWeek.Sunday),

        // 요일은 1(월)~7(일) 로 받는다. .NET 의 열거는 0(일)~6(토) 이라 그대로
        // 견주면 하루씩 밀린다 — 옮기는 자리를 여기 하나로 둔다.
        ReportMailFrequency.Weekly =>
            schedule.DayOfWeek is { } dow && Iso(day.DayOfWeek) == Math.Clamp(dow, 1, 7),

        // **그 달에 없는 날은 말일로 당긴다.** 31 일을 고르면 2월에는 한 번도
        // 안 보내는데, 고른 사람의 뜻은 「달마다 한 번」이다.
        ReportMailFrequency.Monthly =>
            schedule.DayOfMonth is { } dom
            && day.Day == Math.Min(Math.Clamp(dom, 1, 31), DateTime.DaysInMonth(day.Year, day.Month)),

        _ => false,
    };

    /// <summary>.NET 의 요일(0=일)을 ISO 순서(1=월 … 7=일)로.</summary>
    private static int Iso(System.DayOfWeek value) =>
        value == System.DayOfWeek.Sunday ? 7 : (int)value;

    /// <summary>주기를 사람이 읽는 한 줄로. 메일과 화면이 함께 쓴다.</summary>
    public static string Describe(ReportMailSchedule schedule)
    {
        var time = $"{Math.Clamp(schedule.SendHourKst, 0, 23):00}:{Math.Clamp(schedule.SendMinuteKst, 0, 59):00}";

        return schedule.Frequency switch
        {
            ReportMailFrequency.Daily => $"날마다 {time}",
            ReportMailFrequency.Weekday => $"주중(월~금) {time}",
            ReportMailFrequency.Weekly => $"주마다 {DayName(schedule.DayOfWeek)}요일 {time}",
            ReportMailFrequency.Monthly => $"달마다 {schedule.DayOfMonth ?? 1}일 {time}",
            _ => time,
        };
    }

    /// <summary>1(월) ~ 7(일) 을 한 글자로.</summary>
    public static string DayName(int? isoDayOfWeek) => isoDayOfWeek switch
    {
        1 => "월",
        2 => "화",
        3 => "수",
        4 => "목",
        5 => "금",
        6 => "토",
        7 => "일",
        _ => "월",
    };
}
