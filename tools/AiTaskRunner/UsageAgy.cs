using System.Globalization;

namespace AiTaskRunner;

/// <summary>
/// 안티그래비티(<c>agy</c>) 의 <c>/usage</c> 출력을 읽는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>사람이 읽는 문장이 아니라 탭으로 끊은 표다.</b> 그래서 <see cref="UsageText"/>
/// 의 정규식 뭉치를 태우지 않는다 — 태우면 「Weekly Limit <b>Remaining</b> 56%」의
/// 56 을 <i>쓴 비율</i>로 주워 담아, 실제로는 44% 쓴 계정이 화면에 <b>56% 썼다</b>고
/// 앉는다. 숫자가 그럴듯해서 아무도 못 잡는 종류의 거짓말이다.
/// </para>
/// <para>
/// 실제 출력(2026-09-21, 네 칸 · 탭 구분):
/// </para>
/// <code>
/// Gemini Models            Weekly Limit Remaining      56%   2026-09-24T13:30:29Z
/// Gemini Models            Five Hour Limit Remaining   99%   2026-09-21T04:50:51Z
/// Claude and GPT models    Weekly Limit Remaining      28%   2026-09-24T14:39:29Z
/// Claude and GPT models    Five Hour Limit Remaining  100%   2026-09-21T07:27:31Z
/// </code>
/// <para>
/// <b>모델군마다 한도가 따로다.</b> 한 줄로 뭉개면 Gemini 를 다 쓴 것과
/// Claude 를 다 쓴 것이 같은 숫자가 되는데, 이 화면에서 그 둘은 「무엇으로
/// 시킬 것인가」라는 정반대의 답을 부른다. 그래서 모델군을
/// <see cref="AiUsageItem.BucketNm"/> 로 갈라 칸을 따로 낸다.
/// </para>
/// <para>
/// <b>시각은 UTC 로 온다</b>(<c>Z</c>). DB 칸은 시간대가 없는 <c>timestamp</c> 이고
/// 화면도 장비 시각으로 읽으므로 여기서 지역 시각으로 바꿔 넣는다 — 안 바꾸면
/// 「9시간 뒤에 갱신」이 「지금 갱신」으로 보인다.
/// </para>
/// </remarks>
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
            // 탭이 아니라 이어진 공백으로 끊어 오는 판이 있을 수 있어
            // 탭을 먼저 보되, 칸이 모자라면 그 줄은 조용히 버린다.
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

            // **「Remaining」 은 남은 비율이다.** 이 저장소의 나머지가 전부
            // 「쓴 비율」로 말하므로(설계 11.6) 여기서 한 번 뒤집어 맞춘다.
            // 뒤집는 자리를 하나로 두는 것이 중요하다 — 화면에서 뒤집으면
            // 원문과 숫자가 어긋나는 것을 눈으로 확인할 길이 없어진다.
            var used = Math.Clamp(100m - remaining, 0m, 100m);

            var resetAt = cells.Length > 3 ? Local(cells[3].Trim()) : null;

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

            // 창의 뜻은 **낱말**로 정한다. 줄 순서에 기대면 한 줄이 끼거나
            // 빠지는 순간 주간과 5시간이 통째로 뒤바뀐다.
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

    /// <summary>
    /// <c>2026-09-24T13:30:29Z</c> 를 장비의 지역 시각으로.
    /// </summary>
    /// <remarks>
    /// 못 읽으면 <c>null</c> 이다. <b>0 이나 「지금」으로 채우지 않는다</b> —
    /// 「갱신 시각 모름」과 「방금 갱신됨」은 사람이 할 일이 정반대다.
    /// </remarks>
    private static DateTime? Local(string text)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
            ? when.ToLocalTime().DateTime
            : null;
}
