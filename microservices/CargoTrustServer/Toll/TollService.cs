using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Toll;

/// <summary>
/// 계산 엔진과 바깥세상(DB · 차량 · 이력) 사이. <b>엔진은 여전히 아무것도 모른다.</b>
///
/// <para>
/// 여기가 하는 일은 셋이다 — 규칙을 읽어 오고, 차량으로 자격 목록을 만들고,
/// 사람이 읽을 한 줄(<c>Summary</c>)을 짓는다. 그 한 줄을 서버가 짓는 까닭은
/// 화면이 둘(사용자 · 관리자)이고 나중에 셋이 될 수도 있어서다 —
/// 같은 계산을 두고 화면마다 다르게 말하면 그 자체가 틀린 정보가 된다.
/// </para>
/// </summary>
public class TollService(CargoTrustDbContext db, TollRuleStore rules, CurrentUser me)
{
    /// <summary>추천이 기본으로 잡는 소요시간 범위 — 세 시간에서 열두 시간.</summary>
    public const int DefaultMinDuration = 180;
    public const int DefaultMaxDuration = 720;

    // ── 정방향 ───────────────────────────────────────────────

    public async Task<IResult> CalcAsync(TollCalcRequest req, CancellationToken ct)
    {
        if (!Code.TryParse<SectionType>(req.SectionType, out var parsed))
            return ApiError.BadRequest($"sectionType 은 {Code.Allowed<SectionType>()} 중 하나입니다.");
        var section = parsed ?? SectionType.CLOSED;

        var entry = req.EntryAt;
        DateTime? exit = req.ExitAt;

        if (section == SectionType.CLOSED)
        {
            if (exit is null)
                return ApiError.BadRequest("폐쇄식은 진출 시각이 있어야 합니다. 한쪽만 안다면 추천(suggest)을 쓰십시오.");
            if (exit <= entry)
                return ApiError.BadRequest("진출 시각이 진입 시각보다 뒤여야 합니다. 날짜까지 확인하십시오.");
            if ((exit.Value - entry).TotalMinutes > NightDiscountEngine.MaxScanMinutes)
                return ApiError.BadRequest("한 번의 이용시간은 24시간까지 셉니다.");
        }

        var set = await rules.CurrentAsync(DateOnly.FromDateTime(entry), ct);
        var vehicle = await FindVehicleAsync(req.VehicleId, ct);
        var result = NightDiscountEngine.Evaluate(set, section, entry, exit);
        var window = set.WindowFor(section);

        int? delayExit = null, delayEntry = null;
        if (section == SectionType.CLOSED && result.NextBandMinRatio is { } nextRatio && exit is { } e)
            (delayExit, delayEntry) = NightDiscountEngine.StepsToNextBand(window, entry, e, nextRatio);

        var segments = section == SectionType.CLOSED && exit is { } e2
            ? NightDiscountEngine.OverlapSegments(entry, e2, window)
                .Select(s => new TollNightSegment(s.From, s.To)).ToList()
            : [];

        if (req.Save)
        {
            db.TollCalcLogs.Add(new TollCalcLog
            {
                UserId = me.UserId,
                VehicleId = vehicle?.VehicleId,
                Mode = "CALC",
                SectionType = section,
                EntryAt = Kst.ToUtc(entry),
                ExitAt = exit is null ? null : Kst.ToUtc(exit.Value),
                EntryPlazaId = req.EntryPlazaId,
                ExitPlazaId = req.ExitPlazaId,
                TotalMinutes = result.TotalMinutes,
                NightMinutes = result.NightMinutes,
                NightRatio = result.NightRatio,
                DiscountPercent = result.DiscountPercent,
                RuleSetCode = set.Code,
            });
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new TollCalcResult(
            section.ToString(),
            set.Code,
            entry,
            exit,
            Kst.ToUtc(entry),
            exit is null ? null : Kst.ToUtc(exit.Value),
            result.TotalMinutes,
            result.NightMinutes,
            result.NightRatio,
            result.DiscountPercent,
            result.BandMinRatio,
            result.NextBandMinRatio,
            result.NextDiscountPercent,
            delayExit,
            delayEntry,
            WindowLabel(window),
            segments,
            [.. TollEligibility.For(vehicle)],
            CalcSummary(section, result, delayExit, delayEntry)));
    }

    // ── 역방향 ───────────────────────────────────────────────

    public async Task<IResult> SuggestAsync(TollSuggestRequest req, CancellationToken ct)
    {
        if (!Code.TryParse<SectionType>(req.SectionType, out var parsed))
            return ApiError.BadRequest($"sectionType 은 {Code.Allowed<SectionType>()} 중 하나입니다.");
        var section = parsed ?? SectionType.CLOSED;

        var anchorIsEntry = !string.Equals(req.Anchor, "EXIT", StringComparison.OrdinalIgnoreCase);
        if (req.Anchor is not null
            && !string.Equals(req.Anchor, "ENTRY", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(req.Anchor, "EXIT", StringComparison.OrdinalIgnoreCase))
            return ApiError.BadRequest("anchor 는 ENTRY 또는 EXIT 입니다.");

        var set = await rules.CurrentAsync(DateOnly.FromDateTime(req.AnchorAt), ct);
        var window = set.WindowFor(section);
        var bands = set.BandsFor(section);
        var vehicle = await FindVehicleAsync(req.VehicleId, ct);
        var required = NightDiscountEngine.RequiredRatio(bands, req.TargetDiscount);

        if (required is null)
        {
            return ApiError.BadRequest(
                $"지금 규칙({set.Code})으로는 {req.TargetDiscount:0.#}% 할인이 없습니다. "
                + $"가능한 할인율은 {string.Join(" · ", bands.Select(b => $"{b.DiscountPercent:0.#}%").Distinct())} 입니다.");
        }

        // 개방식은 조절할 「이용시간」이 없다. 답은 야간창 그 자체라 추천이 한 토막이다.
        if (section == SectionType.OPEN)
            return Results.Ok(OpenSuggestion(set, window, req, vehicle, required.Value));

        var min = Math.Clamp(req.MinDurationMinutes ?? DefaultMinDuration, 1, NightDiscountEngine.MaxScanMinutes);
        var max = Math.Clamp(req.MaxDurationMinutes ?? Math.Max(min, DefaultMaxDuration), min, NightDiscountEngine.MaxScanMinutes);

        var windows = NightDiscountEngine.Suggest(window, req.AnchorAt, anchorIsEntry, min, max, required.Value);
        var best = NightDiscountEngine.BestReachable(window, req.AnchorAt, anchorIsEntry, min, max);
        var bestBand = NightDiscountEngine.BandFor(bands, best.BestRatio);

        var options = windows.Select(w =>
        {
            var (entry, exit) = anchorIsEntry
                ? (req.AnchorAt, w.BestKst)
                : (w.BestKst, req.AnchorAt);
            var evaluated = NightDiscountEngine.Evaluate(set, section, entry, exit);
            return new TollSuggestOption(
                w.FromKst, w.ToKst, w.BestKst,
                w.MinDurationMinutes, w.MaxDurationMinutes,
                entry, exit,
                evaluated.NightRatio, evaluated.DiscountPercent);
        }).ToList();

        if (req.Save)
        {
            db.TollCalcLogs.Add(new TollCalcLog
            {
                UserId = me.UserId,
                VehicleId = vehicle?.VehicleId,
                Mode = "SUGGEST",
                SectionType = section,
                EntryAt = anchorIsEntry ? Kst.ToUtc(req.AnchorAt) : null,
                ExitAt = anchorIsEntry ? null : Kst.ToUtc(req.AnchorAt),
                TargetDiscount = req.TargetDiscount,
                NightRatio = best.BestRatio,
                DiscountPercent = options.Count > 0 ? req.TargetDiscount : bestBand.DiscountPercent,
                RuleSetCode = set.Code,
            });
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(new TollSuggestResult(
            section.ToString(),
            set.Code,
            anchorIsEntry ? "ENTRY" : "EXIT",
            req.AnchorAt,
            req.TargetDiscount,
            required,
            options.Count > 0,
            options,
            best.BestRatio,
            bestBand.DiscountPercent,
            best.DurationMinutes,
            min, max,
            WindowLabel(window),
            [.. TollEligibility.For(vehicle)],
            SuggestSummary(anchorIsEntry, req.TargetDiscount, required.Value, options, best.BestRatio, bestBand)));
    }

    // ── 규칙 · 영업소 · 이력 ──────────────────────────────────

    public async Task<TollRulesDto> RulesAsync(CancellationToken ct)
    {
        var set = await rules.CurrentAsync(ct);
        return new TollRulesDto(
            set.Code,
            WindowLabel(set.ClosedWindow),
            WindowLabel(set.OpenWindow),
            Clock(set.ClosedWindow.Start),
            Clock(set.ClosedWindow.End),
            Clock(set.OpenWindow.Start),
            Clock(set.OpenWindow.End),
            BandDtos(set.ClosedBands),
            BandDtos(set.OpenBands));
    }

    public async Task<List<TollCalcLogDto>> HistoryAsync(int limit, CancellationToken ct)
    {
        var rows = await db.TollCalcLogs.AsNoTracking()
            .Where(l => l.UserId == me.UserId)
            .OrderByDescending(l => l.CalcId)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

        var vehicleIds = rows.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).Distinct().ToList();
        var plates = await db.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.VehicleId))
            .ToDictionaryAsync(v => v.VehicleId, v => v.PlateNo, ct);

        return rows.Select(l => new TollCalcLogDto(
            l.CalcId, l.Mode, l.SectionType.ToString(),
            l.EntryAt is null ? null : Kst.FromUtc(l.EntryAt.Value).DateTime,
            l.ExitAt is null ? null : Kst.FromUtc(l.ExitAt.Value).DateTime,
            l.TotalMinutes, l.NightMinutes, l.NightRatio, l.DiscountPercent, l.TargetDiscount,
            l.RuleSetCode,
            l.VehicleId is { } vid ? plates.GetValueOrDefault(vid) : null,
            l.CreatedAt)).ToList();
    }

    // ── 사람이 읽을 말 ───────────────────────────────────────

    /// <summary>「21:00」 — 화면이 셈에 쓰는 꼴.</summary>
    private static string Clock(TimeOnly t) => t.ToString("HH\\:mm");

    /// <summary>「21:00~06:00 (9시간)」</summary>
    public static string WindowLabel(NightWindow window) =>
        $"{window.Start:HH\\:mm}~{window.End:HH\\:mm} ({Duration((int)window.Length.TotalMinutes)})";

    /// <summary>「5시간 40분」 — 분으로만 말하면 긴 운행에서 크기가 안 읽힌다.</summary>
    public static string Duration(int minutes)
    {
        if (minutes < 60) return $"{minutes}분";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"{h}시간" : $"{h}시간 {m}분";
    }

    private static List<TollBandDto> BandDtos(IReadOnlyList<DiscountBand> bands)
    {
        var ordered = bands.OrderBy(b => b.MinRatio).ToList();
        return ordered.Select((b, i) =>
        {
            var upper = i + 1 < ordered.Count ? ordered[i + 1].MinRatio : (decimal?)null;
            var label = upper is null
                ? $"야간 {b.MinRatio:0.#}% 이상"
                : $"야간 {b.MinRatio:0.#}% 이상 ~ {upper:0.#}% 미만";
            return new TollBandDto(b.MinRatio, upper, b.DiscountPercent, label);
        }).ToList();
    }

    private static string CalcSummary(SectionType section, NightDiscount r, int? delayExit, int? delayEntry)
    {
        if (section == SectionType.OPEN)
        {
            return r.DiscountPercent > 0
                ? $"개방식 야간창 안을 지납니다 — {r.DiscountPercent:0.#}% 할인."
                : "개방식 야간창 밖이라 심야할인이 없습니다.";
        }

        var head = $"전체 {Duration(r.TotalMinutes)} 중 야간 {Duration(r.NightMinutes)} — "
                   + $"야간 비율 {r.NightRatio:0.##}%, 할인 {r.DiscountPercent:0.#}%.";

        if (r.NextDiscountPercent is not { } next) return head;

        var steps = new List<string>();
        if (delayExit is { } de) steps.Add($"진출을 {Duration(de)} 늦추면");
        if (delayEntry is { } den) steps.Add($"진입을 {Duration(den)} 늦춰도");
        if (steps.Count == 0) return head + $" 다음 단계({next:0.#}%)는 야간 {r.NextBandMinRatio:0.#}% 이상이 필요합니다.";

        return head + $" {string.Join(" / ", steps)} {next:0.#}% 가 됩니다.";
    }

    private static string SuggestSummary(
        bool anchorIsEntry, decimal target, decimal required,
        List<TollSuggestOption> options, decimal bestRatio, DiscountBand bestBand)
    {
        var side = anchorIsEntry ? "진출" : "진입";
        if (options.Count == 0)
        {
            return $"주어진 소요시간 안에서는 {target:0.#}%(야간 {required:0.#}% 이상)에 닿지 못합니다. "
                   + $"가장 좋은 경우가 야간 {bestRatio:0.##}% — {bestBand.DiscountPercent:0.#}% 입니다. "
                   + "소요시간 범위를 넓히거나 기준 시각을 옮겨 보십시오.";
        }

        var first = options[0];
        var range = first.FromKst == first.ToKst
            ? $"{first.FromKst:MM-dd HH:mm}"
            : $"{first.FromKst:MM-dd HH:mm} ~ {first.ToKst:MM-dd HH:mm}";
        return $"{target:0.#}% 를 받으려면 야간 비율이 {required:0.#}% 이상이어야 합니다. "
               + $"{side} 시각을 {range} 사이로 잡으면 됩니다 (권장 {first.BestKst:HH:mm}).";
    }

    private TollSuggestResult OpenSuggestion(
        TollRules set, NightWindow window, TollSuggestRequest req, Vehicle? vehicle, decimal required)
    {
        var (from, to) = NightDiscountEngine.NextOpenWindow(window, req.AnchorAt);
        var inside = NightDiscountEngine.IsInWindow(req.AnchorAt, window);
        var band = NightDiscountEngine.BandFor(set.OpenBands, inside ? 100m : 0m);

        var option = new TollSuggestOption(
            from, to, from, 0, 0, from, from, 100m,
            NightDiscountEngine.BandFor(set.OpenBands, 100m).DiscountPercent);

        return new TollSuggestResult(
            SectionType.OPEN.ToString(), set.Code,
            string.Equals(req.Anchor, "EXIT", StringComparison.OrdinalIgnoreCase) ? "EXIT" : "ENTRY",
            req.AnchorAt, req.TargetDiscount, required,
            true, [option],
            inside ? 100m : 0m, band.DiscountPercent, 0, 0, 0,
            WindowLabel(window),
            [.. TollEligibility.For(vehicle)],
            $"개방식은 통과 시각 한 점으로 봅니다. {from:MM-dd HH:mm}~{to:MM-dd HH:mm} 사이에 지나면 됩니다.");
    }

    private Task<Vehicle?> FindVehicleAsync(long? vehicleId, CancellationToken ct) =>
        vehicleId is { } id
            ? db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == id && v.UserId == me.UserId && !v.IsDeleted, ct)
            : Task.FromResult<Vehicle?>(null);
}
