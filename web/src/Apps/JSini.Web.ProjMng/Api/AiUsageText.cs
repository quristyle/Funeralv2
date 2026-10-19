using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>AI CLI 한도(<see cref="AiUsageSnapshot"/>)를 사람이 읽는 글자로 바꾼다. 화면마다 따로 두면 어긋나도 오류가 안 나므로 여기 모은다.</summary>
public static class AiUsageText
{
    /// <summary>실행기가 재로그인 필요를 알릴 때 까닭 글 맨 앞에 붙이는 표. 실행기 쪽 AuthMark 와 같아야 한다.</summary>
    public const string AuthMark = "[OAUTH]";

    /// <summary>로그인이 끊긴 CLI 의 배지 글자.</summary>
    public const string AuthText = "OAuth 재로그인 필요";

    /// <summary>이 줄이 로그인 끊김인가. 「읽지 못함」과 가른다 — 로그인 끊김은 장비에 붙어 다시 로그인해야 풀린다.</summary>
    public static bool NeedsAuth(AiUsageSnapshot usage)
        => usage.ErrorText?.StartsWith(AuthMark, StringComparison.Ordinal) == true;

    /// <summary>한 CLI 의 줄 가운데 하나라도 로그인이 끊겼나.</summary>
    public static bool NeedsAuth(IEnumerable<AiUsageSnapshot> rows)
        => rows.Any(NeedsAuth);

    /// <summary>까닭을 사람이 읽는 글로. <b>표는 떼어 낸다.</b></summary>
    public static string Why(AiUsageSnapshot usage)
    {
        var text = usage.ErrorText?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.StartsWith(AuthMark, StringComparison.Ordinal)
            ? text[AuthMark.Length..].TrimStart()
            : text;
    }

    /// <summary>로그인 끊김이 코앞이라고 보는 기간(2주). 끊기면 그 CLI 작업도 함께 실패하므로 넉넉히 본다.</summary>
    private static readonly TimeSpan AuthSoon = TimeSpan.FromDays(14);

    /// <summary>로그인 만료가 코앞인가. 이미 지난 것도 여기 든다.</summary>
    public static bool AuthExpiring(AiUsageSnapshot usage)
        => usage.AuthExpiresAt is { } at && at - AppTime.UtcNow <= AuthSoon;

    /// <summary>로그인 만료를 한 줄 글로. 날짜만 적으면 빼기를 해야 해서 남은 기간을 함께 적는다.</summary>
    public static string AuthRemain(AiUsageSnapshot usage)
    {
        if (usage.AuthExpiresAt is not { } at)
        {
            return string.Empty;
        }

        return at <= AppTime.UtcNow
            ? $"로그인 {at.Kst("MM-dd")} 만료됨"
            : $"로그인 {at.Kst("MM-dd")} 만료 · {Remain(at)}";
    }

    /// <summary>한도 값이 오래됐나. 보고 주기(15분)의 네 배를 넘으면 오래된 것으로 본다. UTC 끼리 잰다.</summary>
    public static bool Stale(DateTime observedAt)
        => AppTime.UtcNow - observedAt > TimeSpan.FromMinutes(60);

    /// <summary>갱신 시각이 지났을 때 적는 말. 「0분 남음」이라 적지 않는다 — 새 값이 아직 안 올라온 것이다.</summary>
    private const string SoonText = "곧 갱신";

    /// <summary>갱신까지 지금부터 남은 시간(예: 1일 1시간 30분 남음). 날짜까지 세야 월간 한도가 읽힌다.</summary>
    public static string Remain(DateTime resetAt)
    {
        var left = resetAt - AppTime.UtcNow;

        if (left <= TimeSpan.Zero)
        {
            return SoonText;
        }

        if (left < TimeSpan.FromMinutes(1))
        {
            return "1분 미만 남음";
        }

        // 0 인 칸은 적지 않는다 — 「2일 0시간 0분」은 읽는 품만 늘린다.
        var parts = new List<string>(3);

        if (left.Days > 0) parts.Add($"{left.Days}일");
        if (left.Hours > 0) parts.Add($"{left.Hours}시간");
        if (left.Minutes > 0) parts.Add($"{left.Minutes}분");

        return $"{string.Join(" ", parts)} 남음";
    }

    /// <summary>CLI 이름.</summary>
    public static string Kind(string? kind) => kind switch
    {
        "claude" => "Claude",
        "antigravity" => "안티그래비티",
        "copilot" => "Copilot",
        null or "" => "(없음)",
        _ => kind,
    };

    /// <summary>같은 CLI 안에서 무엇의 한도인가. CLI 가 쓰는 이름을 바꾸지 않아, 원문을 봐도 화면 칸을 바로 짚는다.</summary>
    public static string Bucket(AiUsageSnapshot usage) => usage.BucketNm switch
    {
        null or "" => string.Empty,
        "chat" => "대화",
        "completions" => "자동완성",
        "premium_interactions" => "프리미엄 요청",
        var other => other,
    };

    /// <summary>토큰이냐 크레딧이냐. 같은 말로 적으면 숫자 크기가 엉뚱하게 읽힌다.</summary>
    public static string Unit(string? kind)
        => kind == "copilot" ? "남은 크레딧" : "남은 토큰";

    /// <summary>한 줄에서 가장 많이 쓴 창의 비율. 평균이 아니다 — 하나만 닿아도 막히기 때문이다.</summary>
    public static decimal? TopPct(AiUsageSnapshot usage)
    {
        decimal? top = null;

        foreach (var pct in new[]
                 {
                     usage.SessionPct, usage.WeekPct, usage.WeekOpusPct, usage.MonthPct,
                 })
        {
            if (pct is { } value && (top is null || value > top))
            {
                top = value;
            }
        }

        return top;
    }

    /// <summary>한 CLI 를 통틀어 가장 많이 쓴 비율. 배지 하나로 말하려고 쓴다.</summary>
    public static decimal? TopPct(IEnumerable<AiUsageSnapshot> rows)
    {
        decimal? top = null;

        foreach (var row in rows)
        {
            if (TopPct(row) is { } value && (top is null || value > top))
            {
                top = value;
            }
        }

        return top;
    }

    /// <summary>한도 줄 하나를 한 줄 글로. 없는 칸은 적지 않는다 — 빈 칸을 - 로 채우면 줄이 못 읽게 길어진다.</summary>
    public static string Row(AiUsageSnapshot usage)
    {
        if (!usage.Ok)
        {
            var why = Why(usage) is { Length: > 0 } text ? text : "읽지 못함";
            var head = Bucket(usage);

            return head.Length > 0 ? $"{head} {why}" : why;
        }

        var parts = new List<string>();

        if (usage.SessionPct is { } session) parts.Add($"세션 {session:0.#}%");
        if (usage.WeekPct is { } week) parts.Add($"주간 {week:0.#}%");
        if (usage.WeekOpusPct is { } opus) parts.Add($"주간 Opus {opus:0.#}%");
        if (usage.MonthPct is { } month) parts.Add($"월간 {month:0.#}%");

        if (usage.RemainingTokens is { } left)
        {
            parts.Add(usage.LimitTokens is { } limit
                ? $"{Unit(usage.RunnerKind)} {left:N0}/{limit:N0}"
                : $"{Unit(usage.RunnerKind)} {left:N0}");
        }

        // 읽기는 됐어도 까닭이 있으면(무제한 · 한도 소진) 숫자만으로는 모르니 함께 적는다.
        if (Why(usage) is { Length: > 0 } note)
        {
            parts.Add(note);
        }

        if (parts.Count == 0)
        {
            parts.Add("값 없음");
        }

        var body = string.Join(" · ", parts);
        var bucket = Bucket(usage);

        return bucket.Length > 0 ? $"{bucket} {body}" : body;
    }

    /// <summary>한 CLI 의 한도 줄 전부를 한 줄 글로 잇는다.</summary>
    public static string Summary(IEnumerable<AiUsageSnapshot> rows)
        => string.Join(" / ", rows.Select(Row));

    /// <summary>배지 한 칸 — 글자와 색.</summary>
    /// <param name="Text">배지에 적을 글자.</param>
    /// <param name="Css">공통 배지의 색 수식(<c>jsini-badge--*</c>).</param>
    public readonly record struct Badge(string Text, string Css);

    /// <summary>한 CLI 를 고르개 목록의 배지 하나로. 보고 없음과 다 안 썼음을 가르고, 색은 70%·90% 경계다. 오래된 값은 색을 쓰지 않는다.</summary>
    public static Badge Of(IReadOnlyCollection<AiUsageSnapshot> rows)
    {
        if (rows.Count == 0)
        {
            return new Badge("보고 없음", "jsini-badge--off");
        }

        if (NeedsAuth(rows))
        {
            return new Badge(AuthText, "jsini-badge--err");
        }

        if (rows.Any(r => !r.Ok))
        {
            return new Badge("읽지 못함", "jsini-badge--err");
        }

        var stale = rows.All(r => Stale(r.ObservedAt));

        if (TopPct(rows) is not { } top)
        {
            // 숫자가 없는 것이 맞는 상태(무제한 등)다. 실패처럼 빨강으로 보이면 안 된다.
            var note = rows.Select(Why).FirstOrDefault(t => t.Length > 0);

            return new Badge(string.IsNullOrWhiteSpace(note) ? "한도 값 없음" : note!, "jsini-badge--off");
        }

        var textParts = new List<string>();
        var maxSession = rows.Max(r => r.SessionPct);
        var maxWeek = rows.Max(r => r.WeekPct);
        
        if (maxSession is not null) textParts.Add($"세션 {maxSession:0.#}%");
        if (maxWeek is not null)
        {
            var weekRow = rows.FirstOrDefault(r => r.WeekPct == maxWeek && r.WeekResetAt.HasValue)
                       ?? rows.FirstOrDefault(r => r.WeekResetAt.HasValue);

            var resetText = string.Empty;
            if (weekRow?.WeekResetAt is { } resetAt)
            {
                // 남은 기간은 대시보드 한도 칸과 같은 Remain 으로 짠다 — 갈라 두면 같은 값이 다르게 적힌다.
                var span = Remain(resetAt);
                var rem = span == SoonText ? span : $"갱신까지 {span}";
                resetText = $" <span class=\"pm-usage-reset\">({rem})</span>";
            }

            textParts.Add($"주간 <strong class=\"pm-usage-week-val\">{maxWeek:0.#}%</strong>{resetText}");
        }

        var text = textParts.Count > 0 
            ? string.Join(" / ", textParts)
            : $"사용 {top:0.#}%" + (stale ? " · 오래된 값" : string.Empty);

        var targetPct = TargetPct(rows) ?? top;
        var css = stale ? "jsini-badge--off" : UsageCss(targetPct);

        return new Badge(text, css);
    }

    /// <summary>배지의 색과 수치를 정하는 값. 주간이 있으면 주간, 없으면 가장 높은 것이다. Of 와 Mini 가 같이 쓴다(각자 고르면 색이 어긋난다).</summary>
    private static decimal? TargetPct(IReadOnlyCollection<AiUsageSnapshot> rows)
        => rows.Max(r => r.WeekPct) ?? TopPct(rows);

    /// <summary>한 CLI 를 짧은 배지 하나로. 닫힌 고르개 칸에 쓰며, 풀어 쓴 글은 title 이 맡는다.</summary>
    public static Badge Mini(IReadOnlyCollection<AiUsageSnapshot> rows)
    {
        if (rows.Count == 0)
        {
            return new Badge("보고없음", "jsini-badge--off");
        }

        if (NeedsAuth(rows))
        {
            return new Badge("로그인필요", "jsini-badge--err");
        }

        if (rows.Any(r => !r.Ok))
        {
            return new Badge("읽지못함", "jsini-badge--err");
        }

        if (TargetPct(rows) is not { } pct)
        {
            return new Badge("한도없음", "jsini-badge--off");
        }

        var stale = rows.All(r => Stale(r.ObservedAt));

        return new Badge($"{pct:0}%", stale ? "jsini-badge--off" : UsageCss(pct));
    }

    /// <summary>사용량(%)을 5 단위 단계로 나눠 배지 수식어를 돌려준다.</summary>
    public static string UsageCss(decimal pct)
    {
        var clamped = Math.Clamp(pct, 0m, 100m);
        var step = ((int)Math.Floor(clamped / 5m)) * 5;

        return step switch
        {
            0 => "jsini-badge--usage-0",
            5 => "jsini-badge--usage-5",
            10 => "jsini-badge--usage-10",
            15 => "jsini-badge--usage-15",
            20 => "jsini-badge--usage-20",
            25 => "jsini-badge--usage-25",
            30 => "jsini-badge--usage-30",
            35 => "jsini-badge--usage-35",
            40 => "jsini-badge--usage-40",
            45 => "jsini-badge--usage-45",
            50 => "jsini-badge--usage-50",
            55 => "jsini-badge--usage-55",
            60 => "jsini-badge--usage-60",
            65 => "jsini-badge--usage-65",
            70 => "jsini-badge--usage-70",
            75 => "jsini-badge--usage-75",
            80 => "jsini-badge--usage-80",
            85 => "jsini-badge--usage-85",
            90 => "jsini-badge--usage-90",
            95 => "jsini-badge--usage-95",
            _ => "jsini-badge--usage-100",
        };
    }
}
