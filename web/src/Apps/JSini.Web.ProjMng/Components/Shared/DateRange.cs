namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// <see cref="DateRangeTabs"/> 의 기간 프리셋.
///
/// Vue 스케줄러 원본은 월 이동(이전/다음 달)만 있었다. 일·주는 이 부품을
/// 만들며 넓힌 것이다 — 할일(todo) 계열 화면이 날짜 하루짜리 조회를 쓰므로
/// 프리셋으로 함께 담아 두면 화면마다 날짜 계산을 반복하지 않는다.
/// </summary>
public enum DateRangePreset
{
    /// <summary>하루. 시작 = 끝.</summary>
    Day,

    /// <summary>한 주. 일요일 시작 — 이 포털의 달력이 모두 일요일부터 그린다.</summary>
    Week,

    /// <summary>한 달. 1일부터 말일까지.</summary>
    Month,
}

/// <summary>날짜 구간. 양끝 포함이고 시간은 보지 않는다(자정).</summary>
/// <param name="Start">시작일</param>
/// <param name="End">끝일 (포함)</param>
public readonly record struct DateRange(DateTime Start, DateTime End)
{
    /// <summary>
    /// 기준일이 속한 프리셋 구간을 만든다.
    /// 주는 일요일 시작이다 — 스케줄러 달력의 첫 열과 맞춘다.
    /// </summary>
    public static DateRange Of(DateRangePreset preset, DateTime anchor)
    {
        var day = anchor.Date;
        return preset switch
        {
            DateRangePreset.Day => new DateRange(day, day),
            DateRangePreset.Week => FromSunday(day),
            DateRangePreset.Month => new DateRange(
                new DateTime(day.Year, day.Month, 1),
                new DateTime(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month))),
            _ => new DateRange(day, day),
        };
    }

    private static DateRange FromSunday(DateTime day)
    {
        var start = day.AddDays(-(int)day.DayOfWeek);
        return new DateRange(start, start.AddDays(6));
    }
}
