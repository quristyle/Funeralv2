using System.Globalization;
using System.Text.RegularExpressions;

namespace AiTaskRunner;

/// <summary>
/// CLI 의 /usage 출력에서 숫자를 주워 낸다. 형식이 예고 없이 바뀌므로 원문을 늘 함께 올리고, 줄의 뜻은 순서가 아니라 머리글 낱말로 정하며, 못 읽은 칸은 0 이 아니라 비운다.
/// </summary>
public static partial class UsageText
{
    /// <summary>사용률. <c>45%</c> · <c>45 %</c> 를 받는다.</summary>
    [GeneratedRegex(@"(\d{1,3})(?:\.\d+)?\s*%")]
    private static partial Regex Percent();

    /// <summary>갱신 시각을 알리는 줄. 영어·한국어를 함께 본다.</summary>
    [GeneratedRegex(@"(?:resets?|renews?|refreshes?|갱신|초기화|재설정)\s*(?:on|at|:)?\s*(.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex Reset();

    /// <summary>토큰 잔량. <c>1,234,000 tokens</c> 같은 모양.</summary>
    [GeneratedRegex(@"([\d,]{2,})\s*(?:tokens?|토큰)", RegexOptions.IgnoreCase)]
    private static partial Regex Tokens();

    /// <summary>요금제 이름. <c>Plan: Max</c> · <c>요금제: Max</c>.</summary>
    [GeneratedRegex(@"(?:plan|subscription|요금제)\s*[::]\s*([^\r\n|]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Plan();

    /// <summary>ANSI 색 코드. 출력이 TTY 가 아니어도 섞여 오는 CLI 가 있다.</summary>
    [GeneratedRegex(@"\x1B\[[0-9;?]*[A-Za-z]")]
    private static partial Regex Ansi();

    /// <summary>괄호로 묶인 덧말. <c>(Asia/Seoul)</c> 같은 시간대 표기다.</summary>
    [GeneratedRegex(@"\([^)]*\)?")]
    private static partial Regex Parens();

    /// <summary>이어진 공백. 걷어낸 자리에 남은 것을 하나로 줄인다.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Squeeze();

    /// <summary>어느 한도를 말하는 줄인가.</summary>
    private enum Section
    {
        None,
        Session,
        Week,
        WeekOpus,
    }

    /// <summary>
    /// 출력 한 덩어리를 한도 하나로 옮긴다.
    /// </summary>
    /// <param name="kind">CLI 종류. 그대로 실어 보낸다.</param>
    /// <param name="raw">CLI 가 뱉은 것 전부(표준출력 + 표준오류).</param>
    public static AiUsageItem Parse(string kind, string raw)
    {
        var item = new AiUsageItem { RunnerKind = kind, Ok = true, RawText = raw };

        var text = Ansi().Replace(raw ?? string.Empty, string.Empty);
        var section = Section.None;
        // 여기만 장비의 지역 시각이다 — CLI 는 갱신 시각을 장비 시간대로 찍으므로 같은 시계로 견줘야 「이미 지났으니 내일」 판정이 맞는다.
        // 올릴 때 UTC 로 바꾼다(ParseWhen 끝).
        var now = DateTime.Now;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            // ① 머리글이면 이 아래 줄들이 무엇에 대한 것인지 바꾼다 — 퍼센트가 같은 줄에 있어도 머리글을 먼저 본다.
            var lowered = trimmed.ToLowerInvariant();

            if (lowered.Contains("opus"))
            {
                section = Section.WeekOpus;
            }
            else if (lowered.Contains("week") || trimmed.Contains("주간") || trimmed.Contains("주별"))
            {
                section = Section.Week;
            }
            else if (lowered.Contains("session") || trimmed.Contains("세션"))
            {
                section = Section.Session;
            }

            // ② 퍼센트.
            if (Percent().Match(trimmed) is { Success: true } pct
                && decimal.TryParse(pct.Groups[1].Value, out var value))
            {
                Assign(item, section, value);
            }

            // ③ 갱신 시각.
            if (Reset().Match(trimmed) is { Success: true } reset
                && ParseWhen(reset.Groups[1].Value, now) is { } when)
            {
                AssignReset(item, section, when);
            }

            // ④ 토큰. 「남은」 쪽과 「한도」 쪽을 낱말로 가른다.
            if (Tokens().Match(trimmed) is { Success: true } tok
                && long.TryParse(tok.Groups[1].Value, NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out var tokens))
            {
                if (lowered.Contains("remaining") || lowered.Contains("left") || trimmed.Contains("남은"))
                {
                    item.RemainingTokens = tokens;
                }
                else if (lowered.Contains("limit") || lowered.Contains("total") || trimmed.Contains("한도"))
                {
                    item.LimitTokens = tokens;
                }
            }

            // ⑤ 요금제.
            if (item.PlanNm is null && Plan().Match(trimmed) is { Success: true } plan)
            {
                item.PlanNm = plan.Groups[1].Value.Trim();
            }
        }

        return item;
    }

    private static void Assign(AiUsageItem item, Section section, decimal value)
    {
        switch (section)
        {
            case Section.Session:
                item.SessionPct ??= value;
                break;
            case Section.Week:
                item.WeekPct ??= value;
                break;
            case Section.WeekOpus:
                item.WeekOpusPct ??= value;
                break;
        }
    }

    private static void AssignReset(AiUsageItem item, Section section, DateTime when)
    {
        switch (section)
        {
            case Section.Session:
                item.SessionResetAt ??= when;
                break;
            case Section.Week:
                item.WeekResetAt ??= when;
                break;
            case Section.WeekOpus:
                item.WeekOpusResetAt ??= when;
                break;
        }
    }

    /// <summary>
    /// 「3:00pm」 · 「Nov 5 at 12:00am」 · 「2026-09-21 10:00」 을 시각으로 읽는다. 읽기는 장비의 지역 시각으로, 돌려주기는 UTC 로 한다.
    /// 형식을 먼저 대 보고(TryParse 에만 맡기면 엉뚱하게 읽는다), 시각만 적힌 것은 이미 지났으면 내일로 민다.
    /// </summary>
    /// <param name="now">지금. <b>장비의 지역 시각</b>이어야 한다.</param>
    private static DateTime? ParseWhen(string text, DateTime now)
    {
        // 괄호와 쉼표를 먼저 걷어낸다 — `Sep 21, 12:10pm (Asia/Seoul)` 꼴이 어느 형식에도 안 걸려 갱신 시각이 조용히 비곤 했다. 시간대 이름은 버린다.
        var cleaned = Squeeze().Replace(
                Parens().Replace(text, " ").Replace(',', ' '), " ")
            .Replace(" at ", " ", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .Trim('.', ',', ')', '(', ' ');

        if (cleaned.Length == 0)
        {
            return null;
        }

        var ok = DateTime.TryParseExact(cleaned, Formats, CultureInfo.InvariantCulture,
                     DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(cleaned, CultureInfo.InvariantCulture,
                     DateTimeStyles.AllowWhiteSpaces, out parsed);

        if (!ok)
        {
            return null;
        }

        // 날짜가 안 적혀 있으면 오늘로 채워져 있으니, 그 경우만 미래로 민다.
        var timeOnly = !HasDate(cleaned);

        if (timeOnly && parsed <= now)
        {
            parsed = parsed.AddDays(1);
        }
        else if (!timeOnly && !HasYear(cleaned) && parsed < now.AddMonths(-6))
        {
            // 연도가 없는 「Jan 3」 을 12월에 읽은 경우. 해를 하나 민다.
            parsed = parsed.AddYears(1);
        }

        // 여기까지는 장비의 지역 벽시계다. 올리는 값은 UTC 여야 한다.
        return DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();
    }

    /// <summary>검사할 형식들. 차례가 뜻을 가진다 — 위에서부터 맞는 것이 이긴다.</summary>
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd",
        "yyyy/MM/dd HH:mm", "yyyy/MM/dd",
        "MMM d yyyy h:mmtt", "MMM d yyyy H:mm", "MMM d yyyy",
        "MMM d h:mmtt", "MMM d h:mm tt", "MMM d H:mm", "MMM d htt", "MMM d h tt", "MMM d",
        "MMMM d h:mmtt", "MMMM d H:mm", "MMMM d htt", "MMMM d h tt", "MMMM d",
        "h:mmtt", "h:mm tt", "htt", "h tt", "HH:mm", "H:mm",
    ];

    /// <summary>글자에 날짜가 들어 있나. 숫자-구분자나 달 이름이 보이면 날짜다.</summary>
    private static bool HasDate(string text)
        => text.Contains('-') || text.Contains('/')
            || MonthNames.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase))
            || text.Contains('월');

    /// <summary>네 자리 해가 적혀 있나. 적혀 있으면 해를 밀지 않는다.</summary>
    private static bool HasYear(string text)
        => Year().IsMatch(text);

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex Year();

    private static readonly string[] MonthNames =
    [
        "jan", "feb", "mar", "apr", "may", "jun",
        "jul", "aug", "sep", "oct", "nov", "dec",
    ];
}
