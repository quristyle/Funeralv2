namespace JSini.Web.ProjMng.Api;

/// <summary>
/// AI CLI 한도(<see cref="AiUsageSnapshot"/>)를 사람이 읽는 글자로 바꾼다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 화면 밖에 두나.</b> 같은 값을 이제 두 곳이 그린다 — 「AI 작업 현황」의
/// 한도 칸과 「빠른 지시」의 AI 고르개다. 화면마다 제 <c>switch</c> 를 두면
/// <c>copilot</c> 을 「Copilot」이라 부르는 화면과 「코파일럿」이라 부르는 화면이
/// 생기고, 그 어긋남은 <b>아무 오류도 내지 않는다.</b>
/// </para>
/// <para>
/// <b>퍼센트는 「쓴 비율」이다.</b> 뒤집지 않는다 — 까닭은
/// <see cref="AiUsageSnapshot"/> 머리말에.
/// </para>
/// </remarks>
public static class AiUsageText
{
    /// <summary>
    /// 한도 값이 오래됐나. <b>보고 주기(15분)의 네 배</b>를 기준으로 한다 —
    /// 한두 번 걸러도 소란을 떨지 않되, 반나절 묵은 값을 최신인 척하지 않게.
    /// </summary>
    public static bool Stale(DateTime observedAt)
        => DateTime.Now - observedAt > TimeSpan.FromMinutes(60);

    /// <summary>CLI 이름.</summary>
    public static string Kind(string? kind) => kind switch
    {
        "claude" => "Claude",
        "antigravity" => "안티그래비티",
        "copilot" => "Copilot",
        null or "" => "(없음)",
        _ => kind,
    };

    /// <summary>
    /// 같은 CLI 안에서 무엇의 한도인가. <b>CLI 가 쓰는 이름을 크게 바꾸지 않는다</b> —
    /// 원문을 펴 봤을 때 화면의 어느 칸인지 바로 짚을 수 있어야 한다.
    /// </summary>
    public static string Bucket(AiUsageSnapshot usage) => usage.BucketNm switch
    {
        null or "" => string.Empty,
        "chat" => "대화",
        "completions" => "자동완성",
        "premium_interactions" => "프리미엄 요청",
        var other => other,
    };

    /// <summary>
    /// 토큰이냐 크레딧이냐. 세는 단위가 다른데 같은 말로 적으면 숫자의 크기가
    /// 엉뚱하게 읽힌다 — 200 크레딧과 200 토큰은 전혀 다른 이야기다.
    /// </summary>
    public static string Unit(string? kind)
        => kind == "copilot" ? "남은 크레딧" : "남은 토큰";

    /// <summary>
    /// 한 줄에서 <b>가장 많이 쓴 창</b>의 비율. 세션·주간·주간 Opus·월간 중
    /// 제일 큰 값이다.
    /// </summary>
    /// <remarks>
    /// <b>평균이 아니다.</b> 막히는 것은 넷 중 하나가 100%에 닿는 순간이므로,
    /// 평균을 적으면 「주간이 다 찼는데 화면은 40%」가 된다.
    /// </remarks>
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

    /// <summary>
    /// 한 CLI 를 통틀어 가장 많이 쓴 비율. 한도 줄이 여럿인 CLI
    /// (<c>antigravity</c> · <c>copilot</c>)도 배지 하나로 말하려고 쓴다.
    /// </summary>
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

    /// <summary>
    /// 한도 줄 하나를 한 줄 글로. 예: <c>자동완성 월간 4.9% 남은 크레딧 1,903/2,000</c>.
    /// </summary>
    /// <remarks>
    /// <b>없는 칸은 적지 않는다.</b> CLI 마다 가진 창이 달라서(세션만 있는 것,
    /// 달로만 끊는 것) 빈 칸을 <c>-</c> 로 채우면 한 줄이 못 읽게 길어진다.
    /// </remarks>
    public static string Row(AiUsageSnapshot usage)
    {
        if (!usage.Ok)
        {
            var why = string.IsNullOrWhiteSpace(usage.ErrorText) ? "읽지 못함" : usage.ErrorText!;
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

        // 읽기에 성공했는데 까닭이 적혀 있는 경우 — 「무제한」·「한도 소진됨」처럼
        // **숫자만으로는 못 알아채는** 상태다. 숫자가 있어도 함께 적는다.
        if (usage.ErrorText is { Length: > 0 } note)
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

    /// <summary>
    /// 한 CLI 의 한도 줄 <b>전부</b>를 한 줄 글로 잇는다. 줄이 없으면 빈 글자다.
    /// </summary>
    public static string Summary(IEnumerable<AiUsageSnapshot> rows)
        => string.Join(" / ", rows.Select(Row));

    /// <summary>배지 한 칸 — 글자와 색.</summary>
    /// <param name="Text">배지에 적을 글자.</param>
    /// <param name="Css">공통 배지의 색 수식(<c>jsini-badge--*</c>).</param>
    public readonly record struct Badge(string Text, string Css);

    /// <summary>
    /// 한 CLI 를 배지 하나로. <b>고르개 목록에 다는 것이 이것이다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「보고 없음」과 「다 안 썼음」을 가른다.</b> 올라온 줄이 없는 CLI 를
    /// 0%로 그리면 <i>여유가 넘치는 CLI</i> 처럼 보이는데, 실제로는 실행기가
    /// 그 CLI 를 못 묻고 있는 상태다(대시보드의 같은 칸이 적어 둔 함정이다).
    /// </para>
    /// <para>
    /// 색의 경계는 70% · 90% 다. 오래된 값은 <b>색을 쓰지 않는다</b> — 반나절
    /// 묵은 30%를 초록으로 칠하면 그 초록이 거짓말이 된다.
    /// </para>
    /// </remarks>
    public static Badge Of(IReadOnlyCollection<AiUsageSnapshot> rows)
    {
        if (rows.Count == 0)
        {
            return new Badge("보고 없음", "jsini-badge--off");
        }

        if (rows.Any(r => !r.Ok))
        {
            return new Badge("읽지 못함", "jsini-badge--err");
        }

        var stale = rows.All(r => Stale(r.ObservedAt));

        if (TopPct(rows) is not { } top)
        {
            // 값이 하나도 없다. 「무제한」처럼 **숫자가 없는 것이 맞는** 상태가
            // 여기 온다 — 실패와 같은 빨강으로 보이면 안 된다.
            var note = rows.Select(r => r.ErrorText).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

            return new Badge(string.IsNullOrWhiteSpace(note) ? "한도 값 없음" : note!, "jsini-badge--off");
        }

        var text = $"사용 {top:0.#}%" + (stale ? " · 오래된 값" : string.Empty);

        var css = stale ? "jsini-badge--off"
            : top >= 90 ? "jsini-badge--err"
            : top >= 70 ? "jsini-badge--warn"
            : "jsini-badge--on";

        return new Badge(text, css);
    }
}
