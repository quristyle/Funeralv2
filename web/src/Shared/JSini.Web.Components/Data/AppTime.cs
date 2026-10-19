namespace JSini.Web.Components.Data;

/// <summary>화면의 시계. 서버가 주는 시각은 전부 UTC 다. Blazor Server 에서 DateTime.Now 는 보는 사람이 아니라 서버 프로세스의 시각이라 개발 장비(한국)에서만 아홉 시간 틀린다 — 운영 컨테이너는 UTC 라 맞아서 늦게 들킨다.
/// 재고 견줄 때는 UtcNow, 보여 줄 때는 ToKorea, 달력 날짜 기본값은 Today 를 쓴다. DateTime.Now·Today 는 쓰지 않는다(UtcTimeTests 가 막는다 · docs/utc-time.md).</summary>
public static class AppTime
{
    /// <summary>한국 표준시. 윈도우와 리눅스의 아이디가 달라 둘 다 시도한다.</summary>
    public static readonly TimeZoneInfo Korea = ResolveKorea();

    private static TimeZoneInfo ResolveKorea()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
    }

    /// <summary>지금. <b>언제나 UTC</b> — 서버가 준 시각과 견줄 수 있는 유일한 값이다.</summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>한국 달력의 오늘. 달력 날짜에만 쓴다 — 기간 고르개의 기본값, 날짜 셈.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(ToKorea(DateTime.UtcNow));

    /// <summary>한국 달력의 오늘을 DateTime 으로. DevExpress 날짜 고르개가 DateTime? 를 받으므로 필요하다.</summary>
    public static DateTime TodayDate => Today.ToDateTime(TimeOnly.MinValue);

    /// <summary>UTC 시각 → 한국 벽시계. <b>보여 주기 직전에만</b> 부른다.</summary>
    public static DateTime ToKorea(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Korea);

    /// <summary>UTC 시각 → 한국 벽시계. 값이 없으면 <c>null</c>.</summary>
    public static DateTime? ToKorea(DateTime? utc) => utc.HasValue ? ToKorea(utc.Value) : null;

    /// <summary>한국 벽시계 시각 → UTC. 사람이 고른 기간을 서버에 보낼 때 쓴다.</summary>
    public static DateTime FromKorea(DateTime koreaWallClock)
    {
        var unspecified = DateTime.SpecifyKind(koreaWallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Korea.GetUtcOffset(unspecified)).UtcDateTime;
    }
}

/// <summary>시각을 화면 글자로 바꾸는 확장(AppTime 의 짧은 표기). @at.Kst("MM-dd HH:mm") 처럼 Razor 에서 그대로 읽힌다 — 화면마다 ToKorea 를 적으면 빠뜨리고, 빠뜨린 자리는 아홉 시간 어긋난다.</summary>
public static class AppTimeExtensions
{
    /// <summary>UTC 시각을 한국 시각 글자로.</summary>
    public static string Kst(this DateTime utc, string format) =>
        AppTime.ToKorea(utc).ToString(format);

    /// <summary>UTC 시각을 한국 시각 글자로. 값이 없으면 <paramref name="empty"/>.</summary>
    public static string Kst(this DateTime? utc, string format, string empty = "-") =>
        utc.HasValue ? AppTime.ToKorea(utc.Value).ToString(format) : empty;

    /// <summary>UTC 시각을 한국 벽시계 <see cref="DateTime"/> 으로. 그리드 칸이 받는다.</summary>
    public static DateTime KstTime(this DateTime utc) => AppTime.ToKorea(utc);

    /// <summary>UTC 시각을 한국 벽시계 <see cref="DateTime"/> 으로. 값이 없으면 <c>null</c>.</summary>
    public static DateTime? KstTime(this DateTime? utc) => AppTime.ToKorea(utc);
}
