using System.Globalization;
using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// 코파일럿의 한도 응답(JSON)을 읽는다. 이 CLI 만 CLI 로 묻지 않는다 — /usage 는 -p 로 주면 지시문으로 먹혀 모델이 지어낸 글에서 숫자를 줍게 되므로, 편집기들이 쓰는 한도 주소(AdapterOptions.UsageUrl)를 두드린다.
/// 한도 종류마다 칸을 따로 내고(달로 끊어 MonthPct 칸에 넣는다). 다 쓴 칸은 has_quota 가 false 로 내려오므로 가르는 것은 entitlement 다.
/// </summary>
public static class UsageCopilot
{
    /// <summary>
    /// 응답 한 덩어리를 한도 종류별로 옮긴다.
    /// </summary>
    /// <param name="kind">CLI 종류. 그대로 실어 보낸다.</param>
    /// <param name="raw">받은 본문 전부.</param>
    public static List<AiUsageItem> Parse(string kind, string raw)
    {
        var items = new List<AiUsageItem>();

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var plan = root.TryGetProperty("copilot_plan", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        var resetAt = Utc(root, "quota_reset_date_utc") ?? Utc(root, "quota_reset_date");

        if (!root.TryGetProperty("quota_snapshots", out var snapshots)
            || snapshots.ValueKind != JsonValueKind.Object)
        {
            return items;
        }

        foreach (var snapshot in snapshots.EnumerateObject())
        {
            var value = snapshot.Value;

            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var entitlement = Whole(value, "entitlement");

            // 없는 한도(entitlement 0 · has_quota false)는 「0% 남음」으로 그리지 않는다 — 그리면 늘 빨간 막대가 서 있어 진짜 위험할 때의 빨강이 무의미해진다.
            // 다 쓴 한도와 가르는 것은 entitlement 다: 준 것이 0 이면 애초에 없는 한도, 0 보다 크면 받아 놓고 다 쓴 것이다.
            if (NoQuota(value) && entitlement is null or <= 0)
            {
                continue;
            }

            var item = new AiUsageItem
            {
                RunnerKind = kind,
                BucketNm = snapshot.Name,
                Ok = true,
                PlanNm = plan,
                RawText = raw,
                MonthResetAt = resetAt,
            };

            // 다 쓴 칸을 빼먹으면 화면이 거짓말을 한다 — 바닥난 칸이 빠지고 남은 칸 하나만으로 「사용 4.9%」가 떠 가장 여유로운 CLI 처럼 보인다.
            if (Exhausted(value, entitlement))
            {
                item.MonthPct = 100m;
                item.ErrorText = "한도 소진됨";
                item.LimitTokens = entitlement;

                // 다 쓴 칸의 remaining 은 음수로 온다. 그대로 적으면 화면에 「남은 크레딧 -1」이 앉는다.
                item.RemainingTokens = 0;

                items.Add(item);
                continue;
            }

            if (value.TryGetProperty("unlimited", out var unlimited)
                && unlimited.ValueKind == JsonValueKind.True)
            {
                // 퍼센트가 없는 것이 맞다. 「읽지 못함」과 갈라 두려고 까닭을 적는다.
                item.ErrorText = "무제한";
                items.Add(item);
                continue;
            }

            if (Number(value, "percent_remaining") is { } remaining)
            {
                // 여기도 **남은 비율**이다. 뒤집는 자리를 하나로 둔다(UsageAgy 와 같은 이유).
                item.MonthPct = Math.Clamp(100m - remaining, 0m, 100m);
            }

            // 토큰이 아니라 크레딧이지만 칸의 뜻은 같다 — 「얼마 중 얼마가 남았나」.
            item.LimitTokens = entitlement;
            item.RemainingTokens = Whole(value, "remaining") is { } left ? Math.Max(left, 0L) : null;

            items.Add(item);
        }

        return items;
    }

    /// <summary>이 칸에 지금 쓸 몫이 없는가 — <c>has_quota: false</c>.</summary>
    private static bool NoQuota(JsonElement value)
        => value.TryGetProperty("has_quota", out var has) && has.ValueKind == JsonValueKind.False;

    /// <summary>받아 놓고 다 쓴 칸인가. 응답 모양이 날마다 달라(is_exhausted 가 있거나 없거나) 하나만 보고 판단하지 않는다.</summary>
    private static bool Exhausted(JsonElement value, long? entitlement)
    {
        if (value.TryGetProperty("is_exhausted", out var flag) && flag.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (entitlement is not { } given || given <= 0)
        {
            return false;
        }

        return NoQuota(value) || Whole(value, "remaining") is <= 0;
    }

    private static decimal? Number(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal()
            : null;

    private static long? Whole(JsonElement parent, string name)
        => Number(parent, name) is { } value ? (long)Math.Round(value) : null;

    /// <summary>UTC 로 적힌 시각·날짜를 그대로 UTC 로. 못 읽으면 비운다.</summary>
    private static DateTime? Utc(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
            ? when.UtcDateTime
            : null;
}
