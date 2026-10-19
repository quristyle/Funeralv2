using CargoTrustServer.Common;

namespace CargoTrustServer.Toll;

/// <summary>
/// 야간창 — KST 벽시계 기준의 밤 한 토막. 자정을 넘는다. 폐쇄식 21:00~06:00, 개방식 23:00~05:00.
/// </summary>
public sealed record NightWindow(TimeOnly Start, TimeOnly End)
{
    /// <summary>창의 길이. 끝이 시작보다 작거나 같으면 자정을 넘은 것이다.</summary>
    public TimeSpan Length =>
        End > Start ? End - Start : TimeSpan.FromDays(1) - (Start - End);
}

/// <summary>비율의 띠 — <see cref="MinRatio"/> <b>이상</b>이면 이 띠다.</summary>
public sealed record DiscountBand(decimal MinRatio, decimal DiscountPercent);

/// <summary>지금 쓰는 할인 규칙 한 벌. DB 에서 읽어 만들지만 이 타입은 DB 를 모른다 — 계산을 시험할 때 손으로 만들 수 있어야 한다.</summary>
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

/// <summary>정방향 계산 결과. 할인율을 정하는 값은 NightRatio(야간 이용비율 %) 하나다. 다음 띠 정보는 꼭대기면 null.</summary>
public sealed record NightDiscount(
    SectionType SectionType,
    int TotalMinutes,
    int NightMinutes,
    decimal NightRatio,
    decimal DiscountPercent,
    decimal BandMinRatio,
    decimal? NextBandMinRatio,
    decimal? NextDiscountPercent);

/// <summary>추천 토막 — 「이 사이에 나가면 된다」. FromKst~ToKst 가 상대 시각의 이른·늦은 끝, BestKst 가 가장 짧게 끝나는 권장 시각이다.</summary>
public sealed record SuggestWindow(
    DateTime FromKst,
    DateTime ToKst,
    DateTime BestKst,
    int MinDurationMinutes,
    int MaxDurationMinutes);

/// <summary>
/// 심야할인 계산기. DB 와 「지금」을 모른다. 정·역방향 모두 겹침 함수(OverlapMinutes) 하나에 기대고, 역방향은 1분 간격 훑기로 푼다 — 토막이 여럿 나올 수 있어서다.
/// </summary>
public static class NightDiscountEngine
{
    /// <summary>추천이 훑는 가장 긴 소요시간. 하루를 넘겨 가며 고를 일은 없다.</summary>
    public const int MaxScanMinutes = 24 * 60;

    /// <summary>
    /// startKst~endKst 중 야간창에 든 분. 창이 자정을 넘으므로 하루 전부터 훑는다 — 전날 21시 창이 새벽까지 이어지며, 이 칸을 빼면 새벽 운행의 야간시간이 0 이 된다.
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

    /// <summary>겹친 토막을 그대로 돌려준다 — 타임라인 막대에 음영을 칠한다. 숫자만 주면 어디를 당겨야 올라가는지 모른다.</summary>
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
    /// 진입·진출 시각(KST 벽시계)으로 할인율을 낸다. 개방식은 진출 시각을 쓰지 않는다 — 통과 한 점이 창 안이면 100%, 밖이면 0%.
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
    /// 한쪽 시각을 붙박고 나머지를 추천한다. 폐쇄식 전용 — 개방식은 답이 야간창 그 자체다. minDuration 을 안 받으면 1분 만에 나가는 100% 같은 답이 나온다.
    /// </summary>
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
    /// 소요시간을 붙박고 시각을 옮겨 목표에 닿는 자리를 찾는다. 소요를 늘리면 새벽 진입에서 낮이 늘어 비율이 떨어지므로 이 쪽이 쓸모 있는 「몇 시에 들어가면 되나」다.
    /// </summary>
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

    /// <summary>옮긴 토막을 시각으로 바꾼다. 권하는 Best 는 고른 시각에서 가장 적게 옮기는 자리다.</summary>
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
    /// 지금 운행에서 다음 띠까지 몇 분이 모자란가. 진출을 늦추는 길과 진입을 늦추는 길을 따로 보며, 둘 중 가능한 쪽이 다르다.
    /// </summary>
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
    /// 소요시간 토막을 상대 시각 구간으로 옮긴다. 진입이 붙박이면 소요가 길수록 진출이 늦고, 진출이 붙박이면 진입이 빨라져 이른 끝과 늦은 끝이 뒤집힌다.
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
