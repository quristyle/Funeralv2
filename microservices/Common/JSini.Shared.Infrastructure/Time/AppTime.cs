namespace JSini.Shared.Infrastructure.Time;

/// <summary>
/// 시스템의 시계. 「지금」은 언제나 UTC 다(<see cref="UtcNow"/>). 달력 날짜(<c>date</c>) 칸만 한국 달력으로 센다(<see cref="TodayInKorea"/>).
/// 한국 시각은 보여 주기 직전에 한 번만 만든다 — 중간에 바꾸면 저장으로 흘러가 아홉 시간이 두 번 더해진다. 자세한 것은 docs/utc-time.md.
/// </summary>
public static class AppTime
{
    /// <summary>한국 표준시. 윈도우와 리눅스의 아이디가 달라 둘 다 시도한다.</summary>
    public static readonly TimeZoneInfo Korea = ResolveKorea();

    private static TimeZoneInfo ResolveKorea()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
    }

    /// <summary>지금. <b>언제나 UTC</b>(<c>Kind = Utc</c>).</summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>지금. 시간대를 함께 들고 다녀야 할 때.</summary>
    public static DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;

    /// <summary>
    /// 한국 달력의 오늘. <b>달력 날짜 칸(<c>date</c>)에만 쓴다.</b>
    /// 시각을 견주는 자리에 쓰면 아홉 시간이 섞인다.
    /// </summary>
    public static DateOnly TodayInKorea => DateOnly.FromDateTime(ToKorea(DateTime.UtcNow));

/// <summary>UTC 시각 → 한국 벽시계. <c>Kind</c> 를 보지 않고 UTC 로 읽는다. 보여 주기 직전에만 부른다.</summary>
    public static DateTime ToKorea(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Korea);

    /// <summary>UTC 시각 → 한국 벽시계. 값이 없으면 <c>null</c>.</summary>
    public static DateTime? ToKorea(DateTime? utc) => utc.HasValue ? ToKorea(utc.Value) : null;

    /// <summary>
    /// 한국 벽시계 시각 → UTC. 사람이 고른 기간으로 시각 칸을 거를 때 쓴다.
    /// </summary>
    public static DateTime FromKorea(DateTime koreaWallClock)
    {
        var unspecified = DateTime.SpecifyKind(koreaWallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Korea.GetUtcOffset(unspecified)).UtcDateTime;
    }

    /// <summary>한국 달력 하루의 시작(0시)을 UTC 로.</summary>
    public static DateTime StartOfDayUtc(DateOnly koreaDate) =>
        FromKorea(koreaDate.ToDateTime(TimeOnly.MinValue));
}
