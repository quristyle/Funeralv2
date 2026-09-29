namespace JSini.Shared.Infrastructure.Time;

/// <summary>
/// 시스템의 시계. <b>이 시스템에서 「지금」은 언제나 UTC 다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 규칙은 두 줄이다.
/// </para>
/// <list type="number">
///   <item>
///     <b>순간(instant)은 UTC.</b> DB 의 시각 칸은 전부
///     <c>timestamp with time zone</c> 이고, 거기에 들어가고 나오는 값은
///     모두 UTC 다. 서비스 컨테이너의 시계도 UTC 로 맞춰 두었으므로
///     (<c>deploy/docker</c> 의 <c>TZ=Etc/UTC</c>) <see cref="DateTime.Now"/> 와
///     <see cref="DateTime.UtcNow"/> 가 운영에서는 같은 값이다. 그래도
///     <see cref="UtcNow"/> 를 쓴다 — <b>개발 장비는 한국 시각</b>이라
///     <c>DateTime.Now</c> 로 적은 코드는 거기서만 아홉 시간 어긋나고,
///     그 어긋남은 운영에 올라가서야 사라지므로 아무도 못 본다.
///   </item>
///   <item>
///     <b>달력 날짜(date)는 한국 달력.</b> 계획시작일·목표일·운송일처럼
///     시간대가 아예 없는 값이 있다. 「오늘」이 언제냐는 물음에 UTC 로
///     답하면 한국의 오전 9시 전 아홉 시간 동안 <b>어제</b>가 나온다.
///     그 자리에는 <see cref="TodayInKorea"/> 를 쓴다.
///   </item>
/// </list>
/// <para>
/// 화면에 한국 시각으로 보여 주는 것은 <b>맨 바깥에서 한 번만</b> 한다 —
/// <see cref="ToKorea(DateTime)"/>. 중간 계산에서 바꿔 두면 그 값이 다시
/// 저장으로 흘러가 아홉 시간이 두 번 더해진다.
/// </para>
/// <para>자세한 것은 <c>docs/utc-time.md</c>.</para>
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

    /// <summary>지금. <b>언제나 UTC</b>(<c>Kind = Utc</c>).</summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>지금. 시간대를 함께 들고 다녀야 할 때.</summary>
    public static DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;

    /// <summary>
    /// 한국 달력의 오늘. <b>달력 날짜 칸(<c>date</c>)에만 쓴다.</b>
    /// 시각을 견주는 자리에 쓰면 아홉 시간이 섞인다.
    /// </summary>
    public static DateOnly TodayInKorea => DateOnly.FromDateTime(ToKorea(DateTime.UtcNow));

    /// <summary>
    /// UTC 시각 → 한국 벽시계. <b>보여 주기 직전에만</b> 부른다.
    /// </summary>
    /// <remarks>
    /// <c>Kind</c> 를 따지지 않고 UTC 로 읽는다. <c>Unspecified</c> 로 올라온
    /// 값도 이 시스템에서는 UTC 로 적힌 것이기 때문이다 — 안 맞추면
    /// <see cref="TimeZoneInfo.ConvertTimeFromUtc"/> 가 그것을 현지 시각으로
    /// 오해해 그 칸만 어긋난다.
    /// </remarks>
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
