namespace CargoTrustServer.Common;

/// <summary>
/// 한국 표준시 도우미.
///
/// 운송일·지급일은 날짜(DATE)라 「오늘」이 어느 나라의 오늘인지가 판정을 가른다.
/// 컨테이너 TZ 에 기대지 않고 여기서 KST 로 정한다(LifeEnvServer 와 같은 복사본).
/// **시각은 UTC(DateTimeOffset)로 저장하고, 날짜 판정만 KST 로 한다.**
/// </summary>
public static class Kst
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        // 윈도우("Korea Standard Time")와 리눅스("Asia/Seoul") 아이디가 다르다
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
    }

    /// <summary>지금(KST)</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    /// <summary>UTC → KST</summary>
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>UTC → KST</summary>
    public static DateTimeOffset FromUtc(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, Zone);

    /// <summary>KST 벽시계 시각 → UTC</summary>
    public static DateTimeOffset ToUtc(DateTime kstWallClock)
    {
        var unspecified = DateTime.SpecifyKind(kstWallClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    /// <summary>KST 기준 오늘 0시(UTC 로 환산)</summary>
    public static DateTimeOffset StartOfTodayUtc => ToUtc(Now.Date);
}

/// <summary>KST 날짜 도우미</summary>
public static class KstDate
{
    /// <summary>오늘(KST)</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Kst.Now);

    /// <summary>KST 날짜의 0시를 UTC 로 — 날짜 범위로 시각 열을 거를 때 쓴다.</summary>
    public static DateTimeOffset StartUtc(DateOnly date) => Kst.ToUtc(date.ToDateTime(TimeOnly.MinValue));
}
