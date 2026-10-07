namespace CargoTrustServer.Toll;

// 계약은 docs/cargotrust/06-toll-night-discount.md 다.
//
// [시각 칸은 KST 벽시계다 — 이 서비스에서 유일한 예외다]
//
// 다른 곳은 전부 UTC(DateTimeOffset)로 주고받는다. 여기만 오프셋 없는 DateTime 이고
// 「한국 시각 몇 시」라는 뜻이다. 까닭은 이 업무의 입력이 **본래 한국의 벽시계**이기
// 때문이다 — 기사가 아는 것은 「아홉 시 반에 진입」이지 어떤 순간이 아니다.
//
// 오프셋을 붙여 받으면 길이 둘로 갈린다. 포털 프론트는 컨테이너 시계가 UTC 인
// 서버 안에서 돌아서(TZ=Etc/UTC) 「지역 시각」이 UTC 다. 거기서 만든 DateTime 에
// 오프셋을 붙이면 아홉 시간이 어긋나고, 그 어긋남은 **할인율이 한 띠 밀리는**
// 모습으로만 드러난다. 그래서 들어올 때부터 KST 로 못 박고, 저장(toll_calc_log)과
// 응답의 `...Utc` 칸에서만 UTC 로 바꾼다.

/// <summary>
/// 정방향 — 시각 둘로 할인율을 낸다.
///
/// <para>
/// <c>SectionType</c> 은 <c>CLOSED</c>(기본) · <c>OPEN</c>.
/// <c>EntryAt</c> 은 진입 시각(KST)이고 개방식에서는 통과 시각이다.
/// <c>ExitAt</c> 은 진출 시각(KST) — 개방식에서는 쓰지 않는다.
/// <c>Save</c> 가 참이면 계산 이력에 남긴다.
/// </para>
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
/// 정방향 결과.
///
/// <para>
/// <c>DelayExitMinutes</c> 는 「진출을 이만큼 늦추면 다음 띠」, <c>DelayEntryMinutes</c> 는
/// 「진입을 이만큼 늦춰도 다음 띠」다. 그 길이 없으면 각각 null 이다.
/// </para>
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
/// 역방향 — 한쪽 시각과 목표 할인율로 나머지 한쪽을 추천한다.
///
/// <para>
/// <c>Anchor</c> 는 <c>ENTRY</c>(진입이 정해짐, 기본) 또는 <c>EXIT</c>(진출이 정해짐).
/// </para>
///
/// <para>
/// <c>MinDurationMinutes</c> 는 실제로 걸리는 최소 주행시간이다.
/// <b>이것이 없으면 역산이 성립하지 않는다</b> — 비율의 분모가 소요시간이라,
/// 제한이 없으면 「1분 만에 나가면 100%」가 답이 된다.
/// </para>
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
/// 추천 하나 — 「이 사이면 된다」와 그때의 결과.
///
/// <para>
/// <c>BestKst</c> 는 권장 시각(가장 짧게 끝나는 쪽)이고,
/// <c>PairedEntryKst</c> · <c>PairedExitKst</c> 는 그때의 진입·진출이다.
/// </para>
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
/// 역방향 결과.
///
/// <para>
/// <c>RequiredRatio</c> 는 그 할인율을 받으려면 야간 비율이 얼마 이상이어야 하는가,
/// <c>Reachable</c> 은 주어진 소요시간 범위 안에서 목표에 닿을 수 있는가,
/// <c>BestRatio</c> 는 닿지 못할 때 「그러면 얼마까지 되는가」다.
/// </para>
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
/// 지금 쓰는 규칙. 화면이 「무엇을 기준으로 셌는지」를 적어 두려고 받는다.
///
/// <para>
/// 야간창은 <b>글자와 값 둘 다</b> 싣는다 — <c>…WindowLabel</c> 은 사람에게 보여 줄
/// 한 줄이고, <c>…NightStart</c>·<c>…NightEnd</c>(<c>"21:00"</c> 꼴)는 화면이 셈에 쓴다.
/// 글자에서 시각을 뽑아 쓰면 글자를 다듬는 날 조용히 깨진다.
/// </para>
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
/// 구간 하나의 통행료.
///
/// <para>
/// <c>NormalFare</c> 는 고른 차종의 정상요금, <c>DiscountedFare</c> 는 거기에
/// 우리가 센 심야할인율을 먹인 값이다. <b>할인액을 바깥에서 받아 오지 않는다</b> —
/// 응답의 시간대별 할인요금 칸은 0 으로 오고, 비율로 할인율을 정하는 곳은
/// 한 군데여야 한다.
/// </para>
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
