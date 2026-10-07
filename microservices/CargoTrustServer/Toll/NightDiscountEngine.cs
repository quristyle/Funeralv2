using CargoTrustServer.Common;

namespace CargoTrustServer.Toll;

/// <summary>
/// 야간창 — <b>KST 벽시계</b> 기준의 밤 한 토막. 자정을 넘는다.
///
/// <para>
/// 폐쇄식은 21:00~06:00(9시간), 개방식은 23:00~05:00(6시간)이다.
/// 값은 규칙 묶음(<c>toll_rule_set</c>)이 들고 있고 여기서는 모양만 다룬다.
/// </para>
/// </summary>
public sealed record NightWindow(TimeOnly Start, TimeOnly End)
{
    /// <summary>창의 길이. 끝이 시작보다 작거나 같으면 자정을 넘은 것이다.</summary>
    public TimeSpan Length =>
        End > Start ? End - Start : TimeSpan.FromDays(1) - (Start - End);
}

/// <summary>비율의 띠 — <see cref="MinRatio"/> <b>이상</b>이면 이 띠다.</summary>
public sealed record DiscountBand(decimal MinRatio, decimal DiscountPercent);

/// <summary>
/// 지금 쓰는 할인 규칙 한 벌. DB 에서 읽어 만들지만 <b>이 타입은 DB 를 모른다</b> —
/// 계산을 시험할 때 손으로 만들 수 있어야 한다.
/// </summary>
public sealed record TollRules(
    string Code,
    NightWindow ClosedWindow,
    NightWindow OpenWindow,
    IReadOnlyList<DiscountBand> ClosedBands,
    IReadOnlyList<DiscountBand> OpenBands)
{
    public NightWindow WindowFor(SectionType type) =>
        type == SectionType.CLOSED ? ClosedWindow : OpenWindow;

    /// <summary>작은 비율부터 차례로. 띠 고르기가 이 순서에 기댄다.</summary>
    public IReadOnlyList<DiscountBand> BandsFor(SectionType type) =>
        type == SectionType.CLOSED ? ClosedBands : OpenBands;
}

/// <summary>
/// 정방향 계산의 결과.
///
/// <para>
/// <c>TotalMinutes</c> 는 전체 이용시간(개방식은 한 점이라 0), <c>NightMinutes</c> 는
/// 그중 야간창에 든 시간, <c>NightRatio</c> 는 야간 이용비율(%)이다 —
/// <b>할인율을 정하는 값은 이것 하나다.</b> <c>BandMinRatio</c> 는 적용된 띠의 아래 끝,
/// <c>NextBandMinRatio</c> · <c>NextDiscountPercent</c> 는 다음 띠다(꼭대기면 null).
/// </para>
/// </summary>
public sealed record NightDiscount(
    SectionType SectionType,
    int TotalMinutes,
    int NightMinutes,
    decimal NightRatio,
    decimal DiscountPercent,
    decimal BandMinRatio,
    decimal? NextBandMinRatio,
    decimal? NextDiscountPercent);

/// <summary>
/// 추천 결과 한 토막 — 「이 사이에 나가면 된다」.
///
/// <para>
/// <c>FromKst</c>~<c>ToKst</c> 가 상대 시각의 이른 끝과 늦은 끝(KST)이고,
/// <c>BestKst</c> 는 그중 권장(가장 짧게 끝나는 쪽)이다.
/// <c>MinDurationMinutes</c>~<c>MaxDurationMinutes</c> 는 이 토막의 소요시간 범위다.
/// </para>
/// </summary>
public sealed record SuggestWindow(
    DateTime FromKst,
    DateTime ToKst,
    DateTime BestKst,
    int MinDurationMinutes,
    int MaxDurationMinutes);

/// <summary>
/// 심야할인 계산기. <b>DB 도 「지금」도 모른다.</b>
///
/// <para>
/// [두 업무는 같은 식의 양쪽이다]
/// </para>
///
/// <para>
/// ① 진입·진출 시각 → 할인율, ② 한쪽 시각 + 목표 할인율 → 나머지 시각.
/// 뒤엣것은 앞엣것을 거꾸로 푸는 것이라, 식을 두 벌 두면 반드시 어긋난다.
/// 그래서 겹침을 재는 함수 하나(<see cref="OverlapMinutes"/>)에 모두 기댄다.
/// </para>
///
/// <para>
/// [역방향은 대수가 아니라 훑기로 푼다]
/// </para>
///
/// <para>
/// 야간창이 날마다 되풀이되므로 「비율 ≥ r 을 만족하는 진출시각」은 하나의
/// 구간이 아니라 <b>여러 토막</b>일 수 있다. 식으로 풀면 경계에서 틀리기 쉽고
/// 틀려도 티가 안 난다. 1분 간격으로 훑으면 최대 1440번이고 — 사람이 한 번
/// 누르는 일에 그 정도는 공짜다 — 대신 <b>정방향과 완전히 같은 식</b>을 쓴다.
/// 두 길의 답이 어긋날 수가 없다.
/// </para>
/// </summary>
public static class NightDiscountEngine
{
    /// <summary>추천이 훑는 가장 긴 소요시간. 하루를 넘겨 가며 고를 일은 없다.</summary>
    public const int MaxScanMinutes = 24 * 60;

    /// <summary>
    /// <paramref name="startKst"/>~<paramref name="endKst"/> 중 야간창에 든 분.
    ///
    /// <para>
    /// 창이 자정을 넘으므로 <b>하루 전부터</b> 훑는다 — 전날 21시에 열린 창이
    /// 오늘 새벽까지 이어진다. 이 한 칸을 빠뜨리면 새벽에만 달린 운행의 야간시간이
    /// 통째로 0 이 되고, 그 틀림은 「할인이 안 되네」로만 보인다.
    /// </para>
    /// </summary>
    public static int OverlapMinutes(DateTime startKst, DateTime endKst, NightWindow window)
    {
        if (endKst <= startKst) return 0;

        var total = TimeSpan.Zero;
        var length = window.Length;

        for (var day = startKst.Date.AddDays(-1); day <= endKst.Date; day = day.AddDays(1))
        {
            var windowStart = day + window.Start.ToTimeSpan();
            var windowEnd = windowStart + length;

            var lo = startKst > windowStart ? startKst : windowStart;
            var hi = endKst < windowEnd ? endKst : windowEnd;
            if (hi > lo) total += hi - lo;
        }

        return (int)Math.Round(total.TotalMinutes);
    }

    /// <summary>
    /// 겹친 토막들을 그대로 돌려준다 — 화면이 타임라인 막대에 음영을 칠하는 데 쓴다.
    ///
    /// <para>
    /// 숫자 「63.4%」만 주면 사람은 그 값을 믿거나 말거나 할 뿐이고, 어디를 당겨야
    /// 올라가는지는 모른다. 토막을 그려 주면 둘 다 한눈에 보인다.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(DateTime From, DateTime To)> OverlapSegments(
        DateTime startKst, DateTime endKst, NightWindow window)
    {
        var segments = new List<(DateTime, DateTime)>();
        if (endKst <= startKst) return segments;

        var length = window.Length;
        for (var day = startKst.Date.AddDays(-1); day <= endKst.Date; day = day.AddDays(1))
        {
            var windowStart = day + window.Start.ToTimeSpan();
            var windowEnd = windowStart + length;

            var lo = startKst > windowStart ? startKst : windowStart;
            var hi = endKst < windowEnd ? endKst : windowEnd;
            if (hi > lo) segments.Add((lo, hi));
        }
        return segments;
    }

    /// <summary>한 시각이 야간창 안인가. 개방식 판정이 이것이다.</summary>
    public static bool IsInWindow(DateTime momentKst, NightWindow window)
    {
        var length = window.Length;
        for (var day = momentKst.Date.AddDays(-1); day <= momentKst.Date; day = day.AddDays(1))
        {
            var windowStart = day + window.Start.ToTimeSpan();
            if (momentKst >= windowStart && momentKst < windowStart + length) return true;
        }
        return false;
    }

    /// <summary>비율이 어느 띠에 드는가 — <c>MinRatio</c> 가 비율 이하인 것 중 가장 큰 것.</summary>
    public static DiscountBand BandFor(IReadOnlyList<DiscountBand> bands, decimal ratio)
    {
        DiscountBand? found = null;
        foreach (var band in bands.OrderBy(b => b.MinRatio))
        {
            if (band.MinRatio <= ratio) found = band;
            else break;
        }
        // 띠가 하나도 없거나 0 짜리 띠가 빠진 규칙이면 할인 없음으로 본다.
        return found ?? new DiscountBand(0m, 0m);
    }

    /// <summary>적용된 띠 바로 위의 띠. 이미 꼭대기면 null.</summary>
    public static DiscountBand? NextBandAfter(IReadOnlyList<DiscountBand> bands, decimal currentMinRatio) =>
        bands.Where(b => b.MinRatio > currentMinRatio)
             .OrderBy(b => b.MinRatio)
             .FirstOrDefault();

    /// <summary>
    /// 목표 할인율을 받으려면 비율이 얼마 이상이어야 하는가.
    /// 그 할인율에 닿는 띠가 없으면 null.
    /// </summary>
    public static decimal? RequiredRatio(IReadOnlyList<DiscountBand> bands, decimal targetDiscount)
    {
        var candidates = bands.Where(b => b.DiscountPercent >= targetDiscount).ToList();
        return candidates.Count == 0 ? null : candidates.Min(b => b.MinRatio);
    }

    // ── 정방향 ───────────────────────────────────────────────

    /// <summary>
    /// 진입·진출 시각(KST 벽시계)으로 할인율을 낸다.
    ///
    /// <para>
    /// 개방식은 <paramref name="exitKst"/> 를 쓰지 않는다 — 진·출입 요금소가
    /// 나뉘어 있지 않아 잴 「이용시간」이 없다. 통과 시각 한 점이 창 안이면 100%,
    /// 밖이면 0% 다.
    /// </para>
    /// </summary>
    public static NightDiscount Evaluate(
        TollRules rules, SectionType sectionType, DateTime entryKst, DateTime? exitKst)
    {
        var window = rules.WindowFor(sectionType);
        var bands = rules.BandsFor(sectionType);

        int total, night;
        decimal ratio;

        if (sectionType == SectionType.OPEN)
        {
            total = 0;
            night = 0;
            ratio = IsInWindow(entryKst, window) ? 100m : 0m;
        }
        else
        {
            var exit = exitKst ?? entryKst;
            total = (int)Math.Round((exit - entryKst).TotalMinutes);
            night = OverlapMinutes(entryKst, exit, window);
            ratio = total <= 0 ? 0m : Math.Round(night * 100m / total, 2, MidpointRounding.AwayFromZero);
        }

        var band = BandFor(bands, ratio);
        var next = NextBandAfter(bands, band.MinRatio);

        return new NightDiscount(
            sectionType, total, night, ratio,
            band.DiscountPercent, band.MinRatio,
            next?.MinRatio, next?.DiscountPercent);
    }

    // ── 역방향 ───────────────────────────────────────────────

    /// <summary>
    /// 한쪽 시각을 붙박고 나머지 한쪽을 추천한다. <b>폐쇄식 전용</b> —
    /// 개방식은 조절할 「이용시간」이 없고 답이 야간창 그 자체다.
    /// </summary>
    /// <param name="window">야간창.</param>
    /// <param name="anchorKst">정해진 시각(KST).</param>
    /// <param name="anchorIsEntry">
    /// 참이면 <paramref name="anchorKst"/> 가 진입이고 진출을 찾는다.
    /// 거짓이면 진출이 정해졌고 진입을 찾는다.
    /// </param>
    /// <param name="minDurationMinutes">
    /// 이보다 짧게는 못 간다 — 실제 주행시간이다. 이것을 안 받으면
    /// 「1분 만에 나가면 100%」 같은 답이 나온다.
    /// </param>
    /// <param name="maxDurationMinutes">이보다 오래 끌 생각은 없다.</param>
    /// <param name="requiredRatio">필요한 야간 이용비율(%).</param>
    public static IReadOnlyList<SuggestWindow> Suggest(
        NightWindow window,
        DateTime anchorKst,
        bool anchorIsEntry,
        int minDurationMinutes,
        int maxDurationMinutes,
        decimal requiredRatio)
    {
        var min = Math.Max(1, minDurationMinutes);
        var max = Math.Min(MaxScanMinutes, Math.Max(min, maxDurationMinutes));

        var results = new List<SuggestWindow>();
        int? runStart = null;
        var previous = min - 1;

        for (var d = min; d <= max; d++)
        {
            var (entry, exit) = Span(anchorKst, anchorIsEntry, d);
            var night = OverlapMinutes(entry, exit, window);
            var ratio = Math.Round(night * 100m / d, 2, MidpointRounding.AwayFromZero);
            var ok = ratio >= requiredRatio;

            if (ok && runStart is null) runStart = d;
            if (!ok && runStart is { } s) { results.Add(Window(anchorKst, anchorIsEntry, s, previous)); runStart = null; }
            previous = d;
        }

        if (runStart is { } last) results.Add(Window(anchorKst, anchorIsEntry, last, max));
        return results;
    }

    /// <summary>
    /// <b>소요시간을 붙박고 시각을 옮겨</b> 목표에 닿는 자리를 찾는다.
    ///
    /// <para>
    /// [왜 이것이 따로 필요한가]
    /// </para>
    ///
    /// <para>
    /// <see cref="Suggest"/> 는 고른 시각을 붙박고 <b>소요시간을 늘려</b> 본다.
    /// 그런데 늘리는 것이 늘 좋은 쪽은 아니다 — 새벽 01:40 에 들어가면 창이
    /// 06:00 에 닫히므로, 더 달릴수록 낮이 늘어 비율이 <b>떨어진다.</b> 그때
    /// 「소요시간 범위를 넓혀 보십시오」는 될 리 없는 것을 권하는 말이 된다.
    /// </para>
    ///
    /// <para>
    /// 여섯 시간을 달릴 사람에게 실제로 쓸모 있는 답은 <b>「몇 시에 들어가면
    /// 되는가」</b>이고, 그것은 소요시간을 그대로 두고 시각을 훑으면 나온다.
    /// 쓰는 식은 똑같다 — 겹침 함수 하나에 기댄다.
    /// </para>
    /// </summary>
    /// <param name="aroundKst">사람이 고른 시각. 여기서 가까운 자리를 먼저 권한다.</param>
    /// <param name="anchorIsEntry">그 시각이 진입인가(참) 진출인가(거짓).</param>
    /// <param name="durationMinutes">붙박은 소요시간.</param>
    /// <param name="requiredRatio">필요한 야간 이용비율(%).</param>
    /// <param name="spanMinutes">앞뒤로 얼마나 훑을까. 기본은 하루(앞뒤 12시간)다.</param>
    public static IReadOnlyList<SuggestWindow> SuggestByShift(
        NightWindow window,
        DateTime aroundKst,
        bool anchorIsEntry,
        int durationMinutes,
        decimal requiredRatio,
        int spanMinutes = MaxScanMinutes)
    {
        var duration = Math.Max(1, durationMinutes);
        var half = Math.Max(1, spanMinutes / 2);

        var results = new List<SuggestWindow>();
        int? runStart = null;
        var previous = -half - 1;

        for (var offset = -half; offset <= half; offset++)
        {
            var moved = aroundKst.AddMinutes(offset);
            var (entry, exit) = Span(moved, anchorIsEntry, duration);
            var ratio = Math.Round(OverlapMinutes(entry, exit, window) * 100m / duration, 2, MidpointRounding.AwayFromZero);
            var ok = ratio >= requiredRatio;

            if (ok && runStart is null) runStart = offset;
            if (!ok && runStart is { } s) { results.Add(ShiftWindow(aroundKst, duration, s, previous)); runStart = null; }
            previous = offset;
        }

        if (runStart is { } last) results.Add(ShiftWindow(aroundKst, duration, last, half));
        return results;
    }

    /// <summary>
    /// 옮긴 토막 [<paramref name="fromOffset"/>, <paramref name="toOffset"/>] 을 시각으로.
    ///
    /// <para>
    /// 권하는 한 점(<c>Best</c>)은 <b>고른 시각에서 가장 적게 옮기는 자리</b>다 —
    /// 토막 안에 원래 시각이 들어 있으면 그대로, 아니면 가까운 끝이다.
    /// 「두 시간 당기세요」보다 「십 분만 늦추세요」가 쓸모 있다.
    /// </para>
    /// </summary>
    private static SuggestWindow ShiftWindow(DateTime aroundKst, int duration, int fromOffset, int toOffset)
    {
        var best = fromOffset <= 0 && 0 <= toOffset
            ? 0
            : Math.Abs(fromOffset) <= Math.Abs(toOffset) ? fromOffset : toOffset;

        return new SuggestWindow(
            aroundKst.AddMinutes(fromOffset),
            aroundKst.AddMinutes(toOffset),
            aroundKst.AddMinutes(best),
            duration, duration);
    }

    /// <summary>
    /// 지금 조건에서 받을 수 있는 가장 높은 할인율과 그때의 소요시간.
    /// 목표에 닿지 못할 때 「그러면 얼마까지 되는가」를 말해 주려고 쓴다.
    /// </summary>
    public static (decimal BestRatio, int DurationMinutes) BestReachable(
        NightWindow window, DateTime anchorKst, bool anchorIsEntry,
        int minDurationMinutes, int maxDurationMinutes)
    {
        var min = Math.Max(1, minDurationMinutes);
        var max = Math.Min(MaxScanMinutes, Math.Max(min, maxDurationMinutes));

        var bestRatio = -1m;
        var bestDuration = min;

        for (var d = min; d <= max; d++)
        {
            var (entry, exit) = Span(anchorKst, anchorIsEntry, d);
            var ratio = Math.Round(OverlapMinutes(entry, exit, window) * 100m / d, 2, MidpointRounding.AwayFromZero);
            if (ratio > bestRatio) { bestRatio = ratio; bestDuration = d; }
        }

        return (bestRatio < 0 ? 0m : bestRatio, bestDuration);
    }

    /// <summary>
    /// 지금 짜 둔 운행에서 <b>다음 띠까지 몇 분이 모자란가</b>.
    ///
    /// <para>
    /// 이 화면이 실제로 값을 하는 자리다. 「30%입니다」로 끝나면 계산기지만,
    /// 「진출을 23분 늦추면 50%」까지 말하면 배차를 바꿀 근거가 된다.
    /// 진출을 늦추는 길과 진입을 늦추는 길을 따로 본다 — 둘 중 가능한 쪽이 다르다.
    /// </para>
    /// </summary>
    /// <returns>(진출을 늦출 분, 진입을 늦출 분). 그 길이 없으면 각각 null.</returns>
    public static (int? DelayExit, int? DelayEntry) StepsToNextBand(
        NightWindow window, DateTime entryKst, DateTime exitKst, decimal nextMinRatio)
    {
        var duration = (int)Math.Round((exitKst - entryKst).TotalMinutes);
        if (duration <= 0) return (null, null);

        int? delayExit = null;
        for (var extra = 1; extra <= MaxScanMinutes; extra++)
        {
            var d = duration + extra;
            var ratio = Math.Round(OverlapMinutes(entryKst, entryKst.AddMinutes(d), window) * 100m / d, 2, MidpointRounding.AwayFromZero);
            if (ratio >= nextMinRatio) { delayExit = extra; break; }
        }

        // 진입을 늦추면 운행이 짧아진다 — 진출은 그대로 두고 출발만 미루는 길이다.
        int? delayEntry = null;
        for (var extra = 1; extra < duration; extra++)
        {
            var d = duration - extra;
            var start = entryKst.AddMinutes(extra);
            var ratio = Math.Round(OverlapMinutes(start, exitKst, window) * 100m / d, 2, MidpointRounding.AwayFromZero);
            if (ratio >= nextMinRatio) { delayEntry = extra; break; }
        }

        return (delayExit, delayEntry);
    }

    /// <summary>개방식에서 다음에 열리는 창. 추천 대신 이것을 보여 준다.</summary>
    public static (DateTime FromKst, DateTime ToKst) NextOpenWindow(NightWindow window, DateTime fromKst)
    {
        var length = window.Length;
        for (var day = fromKst.Date.AddDays(-1); day <= fromKst.Date.AddDays(1); day = day.AddDays(1))
        {
            var start = day + window.Start.ToTimeSpan();
            var end = start + length;
            if (fromKst < end) return (start, end);
        }
        var fallback = fromKst.Date.AddDays(1) + window.Start.ToTimeSpan();
        return (fallback, fallback + length);
    }

    /// <summary>붙박은 시각과 소요시간으로 진입·진출 한 쌍을 만든다.</summary>
    public static (DateTime Entry, DateTime Exit) SpanOf(DateTime anchorKst, bool anchorIsEntry, int durationMinutes) =>
        Span(anchorKst, anchorIsEntry, durationMinutes);

    private static (DateTime Entry, DateTime Exit) Span(DateTime anchorKst, bool anchorIsEntry, int durationMinutes) =>
        anchorIsEntry
            ? (anchorKst, anchorKst.AddMinutes(durationMinutes))
            : (anchorKst.AddMinutes(-durationMinutes), anchorKst);

    /// <summary>
    /// 소요시간의 토막 [<paramref name="fromDuration"/>, <paramref name="toDuration"/>] 을
    /// 「상대 시각이 이 사이」로 옮긴다.
    ///
    /// <para>
    /// 진입이 붙박이면 소요시간이 길수록 진출이 늦어지고, 진출이 붙박이면
    /// 소요시간이 길수록 진입이 <b>빨라진다</b> — 이른 끝과 늦은 끝이 뒤집힌다.
    /// </para>
    /// </summary>
    private static SuggestWindow Window(DateTime anchorKst, bool anchorIsEntry, int fromDuration, int toDuration)
    {
        if (anchorIsEntry)
        {
            return new SuggestWindow(
                anchorKst.AddMinutes(fromDuration),
                anchorKst.AddMinutes(toDuration),
                anchorKst.AddMinutes(fromDuration),
                fromDuration, toDuration);
        }

        return new SuggestWindow(
            anchorKst.AddMinutes(-toDuration),
            anchorKst.AddMinutes(-fromDuration),
            anchorKst.AddMinutes(-fromDuration),
            fromDuration, toDuration);
    }
}
