namespace LifeEnvServer.Utilities;

/// <summary>
/// 한국 표준시 도우미.
///
/// 원본(GHUB)은 서버 TZ 에 의존하는 DateTime.Now 와 +9/-9 시간 보정이 섞여 있어
/// 배포 환경에 따라 기상청 발표 차수가 어긋났다. 이식하면서 규칙을 하나로 한다:
/// **저장은 전부 UTC(DateTimeOffset), 기상청 요청 문자열을 만들 때만 KST 로 변환.**
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
