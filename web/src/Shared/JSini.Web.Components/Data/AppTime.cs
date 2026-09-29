namespace JSini.Web.Components.Data;

/// <summary>
/// 화면의 시계. <b>서버가 주는 시각은 전부 UTC 다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 여기가 있는 까닭은 하나다. 이 포털은 <b>Blazor Server</b> 라
/// <see cref="DateTime.Now"/> 가 「보는 사람의 시각」이 아니라
/// <b>서버 프로세스의 시각</b>이다. 그런데 서버가 주는 시각 값은 UTC 이므로
/// <c>DateTime.Now - 서버가준시각</c> 은 개발 장비(한국 시각)에서 아홉 시간
/// 어긋난다. 운영 컨테이너는 <c>TZ=Etc/UTC</c> 라 맞고 개발 장비에서만
/// 틀리므로 — <b>가장 늦게 들키는 종류의 틀림</b>이다.
/// </para>
/// <para>
/// 그래서 화면은 시각을 이렇게 다룬다.
/// </para>
/// <list type="bullet">
///   <item><b>재고 견주기</b>(얼마나 지났나·오래됐나) → <see cref="UtcNow"/></item>
///   <item><b>보여 주기</b>(사람이 읽을 시각) → <see cref="ToKorea(DateTime)"/></item>
///   <item><b>날짜 고르개의 기본값</b>(달력 날짜) → <see cref="Today"/></item>
/// </list>
/// <para>
/// <see cref="DateTime.Now"/> 와 <see cref="DateTime.Today"/> 는 쓰지 않는다.
/// 아키텍처 테스트(<c>UtcTimeTests</c>)가 막는다. 자세한 것은
/// <c>docs/utc-time.md</c>.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 한국 달력의 오늘. <b>달력 날짜에만 쓴다</b> — 기간 고르개의 기본값,
    /// 「며칠 남았나」 같은 날짜 셈.
    /// </summary>
    public static DateOnly Today => DateOnly.FromDateTime(ToKorea(DateTime.UtcNow));

    /// <summary>
    /// 한국 달력의 오늘을 <see cref="DateTime"/> 으로. DevExpress 날짜 고르개가
    /// <c>DateTime?</c> 를 받아서 필요하다.
    /// </summary>
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

/// <summary>
/// 시각을 화면 글자로 바꾸는 확장. <see cref="AppTime"/> 의 짧은 표기다.
/// </summary>
/// <remarks>
/// <c>@at.Kst("MM-dd HH:mm")</c> 처럼 Razor 안에서 그대로 읽히라고 낸 자리다.
/// <c>AppTime.ToKorea(at).ToString("MM-dd HH:mm")</c> 를 화면마다 적으면
/// 길어서 빠뜨리게 된다 — 그리고 빠뜨린 자리는 아홉 시간 어긋난 채로 돈다.
/// </remarks>
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
