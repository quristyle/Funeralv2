namespace HelpDeskServer.Utilities;

/// <summary>
/// 한국 표준시 도우미. (CargoTrustServer · LifeEnvServer 와 같은 복사본)
///
/// <para>
/// 요청·완료 시각은 전부 <b>UTC</b> 로 저장되어 있는데, 현황판이 묻는 것은
/// 「<b>오늘</b> 몇 건 들어왔나」 · 「무슨 <b>요일</b>에 몰리나」 · 「몇 <b>시</b>에
/// 몰리나」다. 그 판정을 UTC 로 하면 한국의 오전 9시 전 아홉 시간이 전날로
/// 밀려, 하루 집계가 통째로 어긋난다. <b>저장은 UTC, 날짜·요일·시간대 판정만
/// KST</b> 로 한다.
/// </para>
///
/// <para>
/// 컨테이너의 TZ 설정에 기대지 않는다 — 운영 컨테이너는 UTC 로 뜬다.
/// </para>
/// </summary>
public static class Kst {
  private static readonly TimeZoneInfo Zone = ResolveZone();

  private static TimeZoneInfo ResolveZone() {
    // 윈도우("Korea Standard Time")와 리눅스("Asia/Seoul") 아이디가 다르다.
    try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
  }

  /// <summary>지금(KST 벽시계)</summary>
  public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

  /// <summary>오늘(KST)</summary>
  public static DateOnly Today => DateOnly.FromDateTime(Now);

  /// <summary>
  /// UTC 시각 → KST 벽시계.
  ///
  /// <para>
  /// <b><c>Kind</c> 를 따지지 않고 전부 UTC 로 읽는다.</b> 이 DB 에는
  /// <c>timestamp with time zone</c>(대부분)과 <c>timestamp without time zone</c>
  /// (<c>usercompletededat</c> 하나)이 섞여 있어서, 앞엣것은 <c>Utc</c> 로
  /// 뒤엣것은 <c>Unspecified</c> 로 올라온다. 둘 다 실제로는 UTC 로 적힌
  /// 값이므로 여기서 한 줄로 맞춘다 — 안 맞추면 <c>ConvertTimeFromUtc</c> 가
  /// <c>Unspecified</c> 를 현지 시각으로 오해해 그 칸만 아홉 시간 어긋난다.
  /// </para>
  /// </summary>
  public static DateTime FromUtc(DateTime value) =>
      TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), Zone);

  /// <summary>UTC 시각 → KST 벽시계. 값이 없으면 null.</summary>
  public static DateTime? FromUtc(DateTime? value) => value.HasValue ? FromUtc(value.Value) : null;

  /// <summary>KST 벽시계 시각 → UTC. 시각 칸을 기간으로 거를 때 쓴다.</summary>
  public static DateTime ToUtc(DateTime kstWallClock) {
    var unspecified = DateTime.SpecifyKind(kstWallClock, DateTimeKind.Unspecified);
    return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified)).UtcDateTime;
  }

  /// <summary>KST 날짜의 0시를 UTC 로.</summary>
  public static DateTime StartUtc(DateOnly date) => ToUtc(date.ToDateTime(TimeOnly.MinValue));
}
