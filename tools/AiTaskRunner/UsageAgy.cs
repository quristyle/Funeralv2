using System.Globalization;

namespace AiTaskRunner;

/// <summary>
/// 안티그래비티(agy) 의 /usage 출력을 읽는다. 탭으로 끊긴 표이므로 UsageText 의 정규식을 태우지 않는다 — 태우면 Remaining(남은 비율)을 쓴 비율로 잘못 주워 담는다.
/// 모델군마다 한도가 따로라 모델군별로 칸을 나눈다. 시각은 UTC 로 와서 지역 시각으로 바꿔 넣는다.
/// </summary>
public static class UsageAgy
{
    /// <summary>
    /// 출력 한 덩어리를 모델군별 한도로 옮긴다.
    /// </summary>
    /// <param name="kind">CLI 종류. 그대로 실어 보낸다.</param>
    /// <param name="raw">CLI 가 뱉은 것 전부.</param>
    public static List<AiUsageItem> Parse(string kind, string raw)
    {
        var items = new List<AiUsageItem>();

        foreach (var line in (raw ?? string.Empty).Split('\n'))
        {
            // 탭이 아니라 이어진 공백으로 끊어 오는 판이 있다 — 탭을 먼저 보고, 칸이 모자라면 그 줄은 버린다.
            var cells = line.Split('\t');

            if (cells.Length < 3)
            {
                continue;
            }

            var group = cells[0].Trim();
            var window = cells[1].Trim();
            var pctText = cells[2].Trim().TrimEnd('%', ' ');

            if (group.Length == 0
                || !decimal.TryParse(pctText, NumberStyles.Number,
                       CultureInfo.InvariantCulture, out var remaining))
            {
                continue;
            }

            // Remaining 은 남은 비율이라 여기서 한 번 뒤집어 「쓴 비율」로 맞춘다. 뒤집는 자리는 이 한 곳이어야 한다.
            var used = Math.Clamp(100m - remaining, 0m, 100m);

            var resetAt = cells.Length > 3 ? Utc(cells[3].Trim()) : null;

            var item = items.FirstOrDefault(i => i.BucketNm == group);

            if (item is null)
            {
                item = new AiUsageItem
                {
                    RunnerKind = kind,
                    BucketNm = group,
                    Ok = true,
                    RawText = raw,
                };

                items.Add(item);
            }

            // 창의 뜻은 낱말로 정한다 — 줄 순서에 기대면 한 줄이 끼거나 빠질 때 주간과 5시간이 뒤바뀐다.
            var lowered = window.ToLowerInvariant();

            if (lowered.Contains("week"))
            {
                item.WeekPct ??= used;
                item.WeekResetAt ??= resetAt;
            }
            else if (lowered.Contains("hour") || lowered.Contains("session"))
            {
                item.SessionPct ??= used;
                item.SessionResetAt ??= resetAt;
            }
        }

        return items;
    }

    /// <summary>ISO 시각(UTC, Z)을 읽는다. 못 읽으면 null — 0 이나 「지금」으로 채우지 않는다(「모름」과 「방금 갱신」은 할 일이 정반대다).</summary>
    private static DateTime? Utc(string text)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
            ? when.UtcDateTime
            : null;
}
