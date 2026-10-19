using CargoTrustServer.Common;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CargoTrustServer.Toll;

/// <summary>할인 규칙을 DB 에서 읽어 TollRules 로 만든다. 규칙은 거의 안 바뀌고 계산은 자주 불리므로 짧게 캐시한다(몇 분이면 관리자 수정이 반영된다).</summary>
public class TollRuleStore(CargoTrustDbContext db, IMemoryCache cache, ILogger<TollRuleStore> logger)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    /// <summary>DB 에 규칙이 하나도 없을 때 쓰는 내장 값. 응답의 ruleSetCode 가 KEC-BASE(내장)로 나와 드러난다 — 조용히 쓰면 스키마 SQL 을 안 돌린 사실이 나중에 분쟁으로 돌아온다.</summary>
    public static readonly TollRules Fallback = new(
        "KEC-BASE(내장)",
        new NightWindow(new TimeOnly(21, 0), new TimeOnly(6, 0)),
        new NightWindow(new TimeOnly(23, 0), new TimeOnly(5, 0)),
        [new(0m, 0m), new(20m, 20m), new(50m, 30m), new(80m, 50m)],
        [new(0m, 0m), new(100m, 50m)]);

    /// <summary>그 날짜에 유효한 규칙. 겹치면 가장 늦게 시작한 것을 쓴다.</summary>
    public async Task<TollRules> CurrentAsync(DateOnly on, CancellationToken ct)
    {
        var key = $"toll-rules:{on:yyyy-MM-dd}";
        if (cache.TryGetValue<TollRules>(key, out var cached) && cached is not null) return cached;

        var set = await db.TollRuleSets.AsNoTracking()
            .Where(r => r.EffectiveFrom <= on && (r.EffectiveTo == null || r.EffectiveTo >= on))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        TollRules rules;
        if (set is null)
        {
            logger.LogWarning("toll_rule_set 에 {On} 에 유효한 규칙이 없다 — 내장값으로 계산한다. "
                              + "deploy/sql/cargotrust-schema-2026-09-24.sql 을 돌렸는지 확인한다.", on);
            rules = Fallback;
        }
        else
        {
            var bands = await db.TollDiscountBands.AsNoTracking()
                .Where(b => b.RuleSetId == set.RuleSetId)
                .OrderBy(b => b.MinRatio)
                .ToListAsync(ct);

            rules = new TollRules(
                set.Code,
                new NightWindow(set.ClosedNightStart, set.ClosedNightEnd),
                new NightWindow(set.OpenNightStart, set.OpenNightEnd),
                Pick(bands, SectionType.CLOSED, Fallback.ClosedBands),
                Pick(bands, SectionType.OPEN, Fallback.OpenBands));
        }

        cache.Set(key, rules, CacheFor);
        return rules;
    }

    /// <summary>지금(KST 오늘) 유효한 규칙.</summary>
    public Task<TollRules> CurrentAsync(CancellationToken ct) => CurrentAsync(KstDate.Today, ct);

    /// <summary>관리자가 규칙을 고친 뒤. 다음 계산부터 새 값을 읽는다.</summary>
    public void Invalidate()
    {
        // IMemoryCache 는 열쇠를 열거하지 못한다. 날짜별 열쇠라 범위를 알 수 없어
        // 오늘 앞뒤 며칠만 지운다 — 계산은 거의 다 오늘 기준이다.
        var today = KstDate.Today;
        for (var offset = -7; offset <= 7; offset++)
            cache.Remove($"toll-rules:{today.AddDays(offset):yyyy-MM-dd}");
    }

    private static IReadOnlyList<DiscountBand> Pick(
        List<TollDiscountBand> bands, SectionType type, IReadOnlyList<DiscountBand> fallback)
    {
        var picked = bands.Where(b => b.SectionType == type)
            .Select(b => new DiscountBand(b.MinRatio, b.DiscountPercent))
            .ToList();
        return picked.Count > 0 ? picked : fallback;
    }
}
