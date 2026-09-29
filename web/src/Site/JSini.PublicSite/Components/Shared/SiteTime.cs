namespace JSini.PublicSite.Components.Shared;

/// <summary>
/// 이 사이트의 시계. <b>프로세스 시계는 UTC 다</b>(<c>deploy/docker</c> 의
/// <c>TZ=Etc/UTC</c> · <c>docs/utc-time.md</c>).
/// </summary>
/// <remarks>
/// 업무 포털의 <c>AppTime</c> 과 같은 규칙이지만 <b>베껴 적는다</b> — 이
/// 사이트는 공유 프로젝트를 하나도 참조하지 않는다(<c>web/CLAUDE.md</c>).
/// 여기서 쓰는 것은 연도 한 곳뿐이라 계약을 하나 늘릴 값어치가 없다.
/// </remarks>
public static class SiteTime
{
    private static readonly TimeZoneInfo Korea = Resolve();

    private static TimeZoneInfo Resolve()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
    }

    /// <summary>
    /// 한국 달력의 오늘. 바닥글의 저작권 연도가 이것을 쓴다 —
    /// UTC 로 적으면 한 해의 첫 아홉 시간 동안 지난해가 적힌다.
    /// </summary>
    public static DateTime TodayInKorea =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Korea).Date;
}
