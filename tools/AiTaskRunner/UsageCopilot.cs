using System.Globalization;
using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// 코파일럿의 한도 응답(JSON)을 읽는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>이 CLI 만 길이 다르다.</b> <c>copilot</c> 의 <c>/usage</c> 는 대화 화면
/// 안에서만 도는 명령이라 <c>-p</c> 로 주면 슬래시 명령이 아니라 <i>지시문</i>으로
/// 먹는다 — 실제로 물어보면 「<c>/usage</c> 가 무엇을 보여 주는지」를 설명하는
/// 모델의 답이 돌아온다. 그것을 파싱하면 화면에 <b>모델이 지어낸 글에서 주운
/// 숫자</b>가 앉는다. 못 읽는 것보다 훨씬 나쁘므로 CLI 로는 아예 묻지 않고,
/// 편집기들이 쓰는 한도 주소를 그대로 두드린다
/// (<c>AdapterOptions.UsageUrl</c>).
/// </para>
/// <para>
/// 실제 응답(2026-09-21, 줄인 것):
/// </para>
/// <code>
/// {
///   "copilot_plan": "individual",
///   "quota_reset_date_utc": "2026-10-01T00:00:00.000Z",
///   "quota_snapshots": {
///     "chat":        { "percent_remaining": 41.6, "entitlement": 200,  "remaining": 83,   "has_quota": true },
///     "completions": { "percent_remaining": 96.5, "entitlement": 2000, "remaining": 1931, "has_quota": true },
///     "premium_interactions": { "has_quota": false, "entitlement": 0 }
///   }
/// }
/// </code>
/// <para>
/// <b>한도 종류마다 칸을 따로 낸다.</b> chat 과 completions 는 서로 다른
/// 주머니라, 합치면 하나가 바닥났는데 평균이 반이라 멀쩡해 보이는 순간이 온다.
/// </para>
/// <para>
/// <b>다 쓰면 그 칸의 <c>has_quota</c> 가 false 로 내려간다</b> — 몫이 아예 없는
/// 칸과 모양이 같아진다. 실제로 본 2026-09-29 의 chat 이 그랬다:
/// </para>
/// <code>
/// "chat": { "percent_remaining": 0.0, "entitlement": 200, "remaining": -1,
///           "credits_used": 200, "has_quota": false }
/// </code>
/// <para>
/// 그래서 <c>has_quota</c> 만 보고 건너뛰면 <b>바닥난 칸이 화면에서 사라지고</b>,
/// 남은 completions 하나만으로 「사용 4.9%」가 되어 <i>가장 여유로운 CLI</i> 처럼
/// 보인다. 가르는 것은 <c>entitlement</c> 다.
/// </para>
/// <para>
/// <b>달로 끊는다.</b> claude·agy 의 세션·주간과 다른 창이라
/// <see cref="AiUsageItem.MonthPct"/> 라는 제 칸에 넣는다 — 주간 칸에 밀어
/// 넣으면 화면의 「주간」이 CLI 마다 다른 기간을 뜻하게 된다.
/// </para>
/// </remarks>
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

            // **없는 한도를 「0% 남음」으로 그리지 않는다.** 이 계정의
            // premium_interactions 가 그 꼴이다(entitlement 0 · has_quota false) —
            // 그대로 그리면 늘 빨간 막대가 하나 서 있고, 그 빨강이 진짜 위험한
            // 날의 빨강을 무의미하게 만든다.
            //
            // **다만 「없는 한도」와 「다 쓴 한도」를 가른다.** 코파일럿은 달치를
            // 다 쓰면 그 칸의 has_quota 를 false 로 내린다 — 몫이 아예 없는 칸과
            // 같은 모양이 된다. 가르는 것은 entitlement 다: 준 것이 0 이면 애초에
            // 없는 한도이고, 0 보다 크면 받아 놓고 다 쓴 것이다.
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

            // **다 쓴 칸을 빼먹으면 화면이 거짓말을 한다.** 2026-09-29 에 실제로
            // 그랬다 — chat 이 바닥나(200/200, has_quota false) 목록에서 빠지고
            // completions 하나만 올라가, 실행하면 「한도 소진」이 나는 CLI 가
            // 화면에는 「사용 4.9%」로 떴다. 가장 여유로운 CLI 처럼 보이는 것이다.
            if (Exhausted(value, entitlement))
            {
                item.MonthPct = 100m;
                item.ErrorText = "한도 소진됨";
                item.LimitTokens = entitlement;

                // 다 쓴 칸의 remaining 은 음수로 온다(chat 은 -1 이었다).
                // 그대로 적으면 화면에 「남은 크레딧 -1」이 앉는다.
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

    /// <summary>
    /// 받아 놓고 다 쓴 칸인가.
    /// </summary>
    /// <remarks>
    /// <b>세 가지 모양을 다 받는다.</b> 응답에 <c>is_exhausted</c> 가 실려 오는 날도
    /// 있고(내부 통로라 칸이 늘고 준다), 실제로 본 2026-09-29 의 응답에는 그 칸이
    /// 없고 <c>has_quota: false · entitlement: 200 · remaining: -1</c> 뿐이었다.
    /// 하나만 보고 판단하면 그 날의 모양이 바뀔 때마다 화면이 다시 조용히 거짓말을 한다.
    /// </remarks>
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
