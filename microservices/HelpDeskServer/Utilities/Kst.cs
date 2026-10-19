namespace HelpDeskServer.Utilities;

/// <summary>한국 표준시 도우미. 저장은 UTC, 날짜·요일·시간대 판정만 KST 로 한다(UTC 로 하면 오전 9시 전이 전날로 밀린다). 컨테이너 TZ 에 기대지 않는다.</summary>
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

  /// <summary>UTC 시각을 KST 벽시계로 바꾼다. Kind 를 따지지 않고 UTC 로 읽는다 — Unspecified 로 오면 현지로 오해해 그 칸만 아홉 시간 어긋난다.</summary>
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
