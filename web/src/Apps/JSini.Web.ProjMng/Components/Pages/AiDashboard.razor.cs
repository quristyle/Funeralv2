using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiDashboard
{
    [Inject] private AiDashboardClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(SchSummary.Period(_from, _to));

    // ── 색 ──────────────────────────────────────────────────
    //
    // 상태색은 **예약이다.** 「계열 4」로 다시 쓰지 않는다 — 다시 쓰면
    // 빨강이 어떤 차트에서는 실패이고 어떤 차트에서는 그냥 넷째가 된다.
    // 그리고 색만으로 뜻을 지지 않게 글자를 늘 옆에 둔다.

    /// <summary>계열 1. 크기를 읽는 막대·선이 쓴다.</summary>
    private const string Series1 = "#2a78d6";

    private const string Good = "#0ca30c";
    private const string Warning = "#fab219";
    private const string Serious = "#ec835a";
    private const string Critical = "#d03b3b";

    /// <summary>회색. 「취소」처럼 좋지도 나쁘지도 않은 것.</summary>
    private const string Muted = "#898781";

    // DevExpress 차트는 계열 색을 **팔레트 순서**로 받는다.
    // 계열마다 `Color` 를 적는 길도 있지만 그쪽은 `System.Drawing.Color` 라
    // 화면에서 hex 를 그대로 쓸 수 없다 — 저장소의 다른 차트도 팔레트를 쓴다.

    /// <summary>계열 하나짜리 차트. 크기를 읽는 자리다.</summary>
    private static readonly string[] SinglePalette = [Series1];

    /// <summary>성공 · 실패. <b>상태색이라 순서를 바꾸면 안 된다.</b></summary>
    private static readonly string[] OutcomePalette = [Good, Critical];

    /// <summary>전체 실행 · 성공. 앞엣것은 크기, 뒤엣것은 상태다.</summary>
    private static readonly string[] MonthlyPalette = [Series1, Good];

    /// <summary>상태 이름 → 색. 도넛의 조각 순서를 여기에 맞춘다.</summary>
    private static readonly Dictionary<string, string> StatusColors = new(StringComparer.Ordinal)
    {
        ["성공"] = Good,
        ["실패"] = Critical,
        ["시간초과"] = Serious,
        ["중단"] = Warning,
        ["취소"] = Muted,
        ["준비 중"] = Series1,
        ["실행 중"] = Series1,
        ["대기"] = Muted,
    };

    // ── 상태 ────────────────────────────────────────────────

    private DateTime? _from = DateTime.Today.AddDays(-29);
    private DateTime? _to = DateTime.Today;

    private AiDashboardData _data = new();
    private AiProviderStatus? _providers;

    private AiDashboardData Data => _data;
    private AiDashboardSummary Summary => _data.Summary;
    private AiDashboardForecast Forecast => _data.Forecast;

    private sealed record Preset(string Text, int Days);

    private static readonly Preset[] Presets =
    [
        new("최근 7일", 7),
        new("최근 30일", 30),
        new("최근 90일", 90),
    ];

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _data = await Api.LoadAsync(_from, _to) ?? new AiDashboardData();

        // **곁들이는 값이다.** 못 읽어도 대시보드는 그린다 — 클라이언트가
        // 이미 삼키고 null 을 준다.
        _providers = await Api.ProvidersAsync();

        // 0 건이어도 화면은 타일과 빈 차트를 그린다. 그것이 사실이다.
        // 「없습니다」는 이 기간에 아무 실행도 없을 때만 띄운다.
        return _data.Summary.Runs;
    }, "이 기간에 처리한 작업이 없습니다.", "현황을 읽지 못했습니다");

    private Task ApplyPresetAsync(int days)
    {
        _to = DateTime.Today;
        _from = DateTime.Today.AddDays(-(days - 1));
        return SearchAsync();
    }

    // ── 도넛 ────────────────────────────────────────────────

    /// <summary>
    /// 상태 조각. <b>0 건인 상태는 뺀다</b> — 조각은 안 그려지는데 범례만
    /// 남아 도넛과 범례가 어긋나 보인다.
    /// </summary>
    private List<AiDashboardSlice> StatusSlices =>
    [
        .. Data.ByStatus
            .Where(s => s.Runs > 0)
            .Select(s => new AiDashboardSlice
            {
                Label = StatusText(s.Label),
                Runs = s.Runs,
                Succeeded = s.Succeeded,
                Failed = s.Failed,
                AvgMinutes = s.AvgMinutes,
            })
    ];

    /// <summary>조각 색. <see cref="StatusSlices"/> 와 같은 순서여야 어긋나지 않는다.</summary>
    private string[] StatusPalette =>
        [.. StatusSlices.Select(s => StatusColors.GetValueOrDefault(s.Label, Muted))];

    /// <summary>
    /// 계량기 색. <b>색만으로 뜻을 지지 않는다</b> — 퍼센트 글자가 늘 옆에 있다.
    /// </summary>
    private static string MeterColor(decimal pct)
        => pct >= 90 ? Critical : pct >= 75 ? Serious : pct >= 50 ? Warning : Good;

    // ── 글자 ────────────────────────────────────────────────

    private static string Span(decimal seconds)
    {
        if (seconds <= 0)
        {
            return "—";
        }

        var span = TimeSpan.FromSeconds((double)seconds);

        return span.TotalHours >= 1 ? $"{span.TotalHours:0.#}시간"
            : span.TotalMinutes >= 1 ? $"{span.TotalMinutes:0.#}분"
            : $"{span.TotalSeconds:0}초";
    }

    private static string Ago(DateTime at)
    {
        var gap = DateTime.Now - at;

        return gap < TimeSpan.Zero ? at.ToString("MM-dd HH:mm")
            : gap.TotalMinutes < 1 ? "방금"
            : gap.TotalHours < 1 ? $"{gap.TotalMinutes:0}분 전"
            : gap.TotalDays < 1 ? $"{gap.TotalHours:0}시간 전"
            : at.ToString("MM-dd HH:mm");
    }

    /// <summary>
    /// 한도 값이 오래됐나. <b>보고 주기(15분)의 네 배</b>를 기준으로 한다 —
    /// 한두 번 걸러도 소란을 떨지 않되, 반나절 묵은 값을 최신인 척하지 않게.
    /// </summary>
    private static bool Stale(DateTime observedAt)
        => DateTime.Now - observedAt > TimeSpan.FromMinutes(60);

    private string MomText => Forecast.MonthOverMonth is { } mom
        ? $"{(mom >= 0 ? "+" : string.Empty)}{mom:0.#}%"
        : "비교할 자료 없음";

    /// <summary>한 줄이 전체에서 차지하는 몫(%). 표 안의 얇은 막대가 쓴다.</summary>
    private static string Share(int runs, List<AiDashboardSlice> rows)
    {
        var top = rows.Count == 0 ? 0 : rows.Max(r => r.Runs);
        return top <= 0 ? "0" : (runs * 100.0 / top).ToString("0.#");
    }

    /// <summary>
    /// 한도 칸에 늘 자리를 내 주는 CLI.
    /// </summary>
    /// <remarks>
    /// <b>올라온 것만 그리지 않는다.</b> 그러면 「이 CLI 는 안 쓴다」와
    /// 「이 CLI 의 보고가 끊겼다」가 화면에서 똑같이 <i>없음</i>으로 보인다 —
    /// 실제로 antigravity·copilot 이 묻는 길을 못 찾은 채 몇 주를 그렇게
    /// 비어 있었고, 아무도 고장으로 읽지 않았다.
    /// <para>여기 없는 종류가 올라오면 그것도 그대로 그린다(아래 UsageKinds).</para>
    /// </remarks>
    private static readonly string[] KnownKinds = ["claude", "antigravity", "copilot"];

    /// <summary>그릴 차례. 아는 셋을 먼저 두고, 처음 보는 종류를 뒤에 붙인다.</summary>
    private IEnumerable<string> UsageKinds =>
        KnownKinds.Concat(
            Data.Usage
                .Select(u => u.RunnerKind ?? string.Empty)
                .Where(k => k.Length > 0
                    && !KnownKinds.Contains(k, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string KindText(string? kind) => kind switch
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
    private static string BucketText(AiUsageSnapshot usage) => usage.BucketNm switch
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
    private static string UnitText(string? kind)
        => kind == "copilot" ? "남은 크레딧" : "남은 토큰";

    private static string StatusText(string? status) => status switch
    {
        "succeeded" => "성공",
        "failed" => "실패",
        "timeout" => "시간초과",
        "canceled" => "취소",
        "interrupted" => "중단",
        "preparing" => "준비 중",
        "running" => "실행 중",
        "queued" => "대기",
        null or "" => "(없음)",
        _ => status,
    };

    private static string StatusClass(string? status) => status switch
    {
        "succeeded" => "jsini-badge--on",
        "failed" or "timeout" => "jsini-badge--err",
        "interrupted" => "jsini-badge--warn",
        _ => "jsini-badge--off",
    };
}
