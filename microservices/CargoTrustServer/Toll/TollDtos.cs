namespace CargoTrustServer.Toll;

// 계약은 docs/cargotrust/06-toll-night-discount.md 다. 시각 칸은 KST 벽시계다(이 서비스의 유일한 예외).
// 오프셋을 붙여 받으면 UTC 서버의 지역 시각이 아홉 시간 어긋나 할인율이 한 띠 밀린다 — 들어올 때 KST 로 못 박고, 저장과 ...Utc 칸에서만 UTC 로 바꾼다.

/// <summary>
/// 정방향 — 시각 둘로 할인율을 낸다. EntryAt 은 진입(개방식은 통과) KST, ExitAt 은 진출 KST(개방식은 안 씀). Save 가 참이면 이력에 남긴다.
/// </summary>
public record TollCalcRequest(
    string? SectionType,
    DateTime EntryAt,
    DateTime? ExitAt,
    long? VehicleId,
    long? EntryPlazaId,
    long? ExitPlazaId,
    bool Save = false);

/// <summary>야간창에 겹친 토막 — 화면의 타임라인 음영.</summary>
public record TollNightSegment(DateTime FromKst, DateTime ToKst);

/// <summary>
/// 정방향 결과. DelayExitMinutes 는 진출을 이만큼 늦추면 다음 띠, DelayEntryMinutes 는 진입을 늦춰도 다음 띠다. 길이 없으면 null.
/// </summary>
public record TollCalcResult(
    string SectionType,
    string RuleSetCode,
    DateTime EntryAtKst,
    DateTime? ExitAtKst,
    DateTimeOffset EntryAtUtc,
    DateTimeOffset? ExitAtUtc,
    int TotalMinutes,
    int NightMinutes,
    decimal NightRatio,
    decimal DiscountPercent,
    decimal BandMinRatio,
    decimal? NextBandMinRatio,
    decimal? NextDiscountPercent,
    int? DelayExitMinutes,
    int? DelayEntryMinutes,
    string NightWindowLabel,
    List<TollNightSegment> NightSegments,
    List<EligibilityCheck> Checks,
    string EligibilityVerdict,
    string EligibilityNote,
    string Summary);

/// <summary>
/// 역방향 — 한쪽 시각과 목표 할인율로 나머지를 추천한다. MinDurationMinutes 가 없으면 「1분 만에 나가면 100%」가 답이 되므로 반드시 받는다.
/// </summary>
public record TollSuggestRequest(
    string? SectionType,
    string? Anchor,
    DateTime AnchorAt,
    decimal TargetDiscount,
    int? MinDurationMinutes,
    int? MaxDurationMinutes,
    long? VehicleId,
    bool Save = false);

/// <summary>
/// 추천 하나 — 「이 사이면 된다」. BestKst 는 가장 짧게 끝나는 권장 시각, PairedEntryKst·PairedExitKst 는 그때의 진입·진출이다.
/// </summary>
public record TollSuggestOption(
    DateTime FromKst,
    DateTime ToKst,
    DateTime BestKst,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    DateTime PairedEntryKst,
    DateTime PairedExitKst,
    decimal NightRatio,
    decimal DiscountPercent);

/// <summary>
/// 역방향 결과. RequiredRatio 는 그 할인율의 필요 야간 비율, Reachable 은 소요 범위 안에서 닿는지, BestRatio 는 못 닿을 때 얼마까지 되는지다.
/// </summary>
public record TollSuggestResult(
    string SectionType,
    string RuleSetCode,
    string Anchor,
    DateTime AnchorAtKst,
    decimal TargetDiscount,
    decimal? RequiredRatio,
    bool Reachable,
    string Mode,
    List<TollSuggestOption> Options,
    decimal BestRatio,
    decimal BestDiscountPercent,
    int BestDurationMinutes,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    string NightWindowLabel,
    List<EligibilityCheck> Checks,
    string EligibilityVerdict,
    string EligibilityNote,
    string Summary);

/// <summary>할인 띠 하나 — 화면이 목표 할인율 고르개를 만들 때 쓴다.</summary>
public record TollBandDto(decimal MinRatio, decimal? MaxRatioExclusive, decimal DiscountPercent, string Label);

/// <summary>
/// 지금 쓰는 규칙. 야간창은 글자(…WindowLabel)와 값(…NightStart·…NightEnd)을 둘 다 싣는다 — 셈은 값으로 하고, 글자에서 시각을 뽑으면 글자를 다듬는 날 깨진다.
/// </summary>
public record TollRulesDto(
    string Code,
    string ClosedWindowLabel,
    string OpenWindowLabel,
    string ClosedNightStart,
    string ClosedNightEnd,
    string OpenNightStart,
    string OpenNightEnd,
    List<TollBandDto> ClosedBands,
    List<TollBandDto> OpenBands);

/// <summary>영업소.</summary>
public record TollPlazaDto(
    long PlazaId,
    string UnitCode,
    string UnitName,
    string? RouteNo,
    string? RouteName,
    string SectionType,
    bool IsPrivate,
    decimal? Lat,
    decimal? Lon);

/// <summary>영업소 한 줄 올리기 — 동기화와 손입력이 같은 모양을 쓴다.</summary>
public record TollPlazaUpsert(
    string UnitCode,
    string UnitName,
    string? RouteNo,
    string? RouteName,
    string? SectionType,
    bool IsPrivate = false,
    decimal? Lat = null,
    decimal? Lon = null,
    bool IsActive = true);

/// <summary>동기화 결과.</summary>
public record TollPlazaSyncResult(int Received, int Created, int Updated, int Skipped, string Source, string Message);

/// <summary>계산 이력 한 줄.</summary>
public record TollCalcLogDto(
    long CalcId,
    string Mode,
    string SectionType,
    DateTime? EntryAtKst,
    DateTime? ExitAtKst,
    int? TotalMinutes,
    int? NightMinutes,
    decimal? NightRatio,
    decimal? DiscountPercent,
    decimal? TargetDiscount,
    string? RuleSetCode,
    string? PlateNo,
    DateTimeOffset CreatedAt);

/// <summary>
/// 구간 하나의 통행료. DiscountedFare 는 정상요금에 우리가 센 할인율을 먹인 값이다 — 할인액은 바깥에서 받지 않는다(응답의 odn*Dc 칸은 0).
/// </summary>
public record TollFareDto(
    string FromCode,
    string FromName,
    string ToCode,
    string ToName,
    decimal DistanceKm,
    int DriveMinutes,
    string VehicleClass,
    string VehicleClassName,
    int? NormalFare,
    int? DiscountedFare,
    int? SavedFare,
    decimal DiscountPercent,
    string Summary);
