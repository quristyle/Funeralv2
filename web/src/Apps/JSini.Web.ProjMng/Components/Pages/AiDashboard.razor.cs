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
    // 상태색은 예약이다 — 「계열 4」로 다시 쓰지 않는다(차트마다 빨강의 뜻이 달라진다). 색만으로 뜻을 지지 않게 글자를 옆에 둔다.

    /// <summary>계열 1. 크기를 읽는 막대·선이 쓴다.</summary>
    private const string Series1 = "#2a78d6";

    private const string Good = "#0ca30c";
    private const string Warning = "#fab219";
    private const string Serious = "#ec835a";
    private const string Critical = "#d03b3b";

    /// <summary>회색. 「취소」처럼 좋지도 나쁘지도 않은 것.</summary>
    private const string Muted = "#898781";

    // DevExpress 차트는 계열 색을 팔레트 순서로 받는다. Color 를 적는 길은 System.Drawing.Color 라 hex 를 쓸 수 없어 팔레트를 쓴다.

    /// <summary>계열 하나짜리 차트. 크기를 읽는 자리다.</summary>
    private static readonly string[] SinglePalette = [Series1];

    /// <summary>성공 · 실패. <b>상태색이라 순서를 바꾸면 안 된다.</b></summary>
    private static readonly string[] OutcomePalette = [Good, Critical];

    /// <summary>전체 실행 · 성공. 앞엣것은 크기, 뒤엣것은 상태다.</summary>
    private static readonly string[] MonthlyPalette = [Series1, Good];

    /// <summary>AI 모델 사용량 추이 계열 색 (Claude · 안티그래비티 · Copilot).</summary>
    private static readonly string[] UsageTrendPalette = ["#8b5cf6", "#0284c7", "#10b981"];

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

    // 기간 고르개는 달력 날짜를 다룬다. 서버도 한국 달력으로 센다(AiDashboardService.StartedKst).
    private DateTime? _from = AppTime.TodayDate.AddDays(-29);
    private DateTime? _to = AppTime.TodayDate;

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

        // 곁들이는 값이다. 못 읽어도 대시보드는 그린다 — 클라이언트가 삼키고 null 을 준다.
        _providers = await Api.ProvidersAsync();

        // 0 건이어도 타일과 빈 차트를 그린다. 「없습니다」는 이 기간에 실행이 없을 때만 띄운다.
        return _data.Summary.Runs;
    }, "이 기간에 처리한 작업이 없습니다.", "현황을 읽지 못했습니다");

    private Task ApplyPresetAsync(int days)
    {
        _to = AppTime.TodayDate;
        _from = AppTime.TodayDate.AddDays(-(days - 1));
        return SearchAsync();
    }

    // ── 도넛 ────────────────────────────────────────────────

    /// <summary>상태 조각. 0 건인 상태는 뺀다 — 조각은 안 그려지는데 범례만 남으면 도넛과 범례가 어긋난다.</summary>
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

    /// <summary>계량기 색. 색만으로 뜻을 지지 않는다 — 퍼센트 글자가 늘 옆에 있다.</summary>
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

    private static string TokensText(long tokens)
    {
        if (tokens <= 0)
        {
            return "0";
        }

        return tokens.ToString("N0");
    }

    /// <summary>「몇 분 전」. at 은 서버가 준 UTC 라 견주기는 UTC 끼리 하고, 적을 때만 한국 시각으로 옮긴다.</summary>
    private static string Ago(DateTime at)
    {
        var gap = AppTime.UtcNow - at;

        return gap < TimeSpan.Zero ? at.Kst("MM-dd HH:mm")
            : gap.TotalMinutes < 1 ? "방금"
            : gap.TotalHours < 1 ? $"{gap.TotalMinutes:0}분 전"
            : gap.TotalDays < 1 ? $"{gap.TotalHours:0}시간 전"
            : at.Kst("MM-dd HH:mm");
    }

    /// <summary>한도 값이 오래됐나. 「빠른 지시」의 고르개와 같은 잣대(AiUsageText)를 쓴다 — 두 곳이 다르게 판정하면 둘 다 못 믿게 된다.</summary>
    private static bool Stale(DateTime observedAt) => AiUsageText.Stale(observedAt);

    /// <summary>갱신까지 남은 시간. 「빠른 지시」와 같은 글자를 쓰며(AiUsageText.Remain), 그려지는 순간을 기준으로 센다 — 그래서 절대 시각을 옆에 함께 적는다.</summary>
    private static string Remain(DateTime resetAt) => AiUsageText.Remain(resetAt);

    private string MomText => Forecast.MonthOverMonth is { } mom
        ? $"{(mom >= 0 ? "+" : string.Empty)}{mom:0.#}%"
        : "비교할 자료 없음";

    /// <summary>한 줄이 전체에서 차지하는 몫(%). 표 안의 얇은 막대가 쓴다.</summary>
    private static string Share(int runs, List<AiDashboardSlice> rows)
    {
        var top = rows.Count == 0 ? 0 : rows.Max(r => r.Runs);
        return top <= 0 ? "0" : (runs * 100.0 / top).ToString("0.#");
    }

    /// <summary>한도 칸에 늘 자리를 내 주는 CLI. 올라온 것만 그리지 않는다 — 그러면 「안 쓴다」와 「보고가 끊겼다」가 똑같이 없음으로 보인다. 여기 없는 종류가 올라오면 그대로 그린다(UsageKinds).</summary>
    private static readonly string[] KnownKinds = ["claude", "antigravity", "copilot"];

    /// <summary>그릴 차례. 아는 셋을 먼저 두고, 처음 보는 종류를 뒤에 붙인다.</summary>
    private IEnumerable<string> UsageKinds =>
        KnownKinds.Concat(
            Data.Usage
                .Select(u => u.RunnerKind ?? string.Empty)
                .Where(k => k.Length > 0
                    && !KnownKinds.Contains(k, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string KindText(string? kind) => AiUsageText.Kind(kind);

    /// <summary>이 줄이 「로그인이 끊겼다」인가. 「빠른 지시」의 고르개와 같은 잣대다(AiUsageText.NeedsAuth).</summary>
    private static bool NeedsAuth(AiUsageSnapshot usage) => AiUsageText.NeedsAuth(usage);

    /// <summary>까닭을 사람이 읽는 글로 — 기계가 보는 표는 떼어 낸다.</summary>
    private static string Why(AiUsageSnapshot usage) => AiUsageText.Why(usage);

    /// <summary>같은 CLI 안에서 무엇의 한도인가. CLI 가 쓰는 이름을 크게 바꾸지 않는다 — 원문을 펴 봤을 때 화면의 어느 칸인지 바로 짚을 수 있어야 한다.</summary>
    private static string BucketText(AiUsageSnapshot usage) => AiUsageText.Bucket(usage);

    /// <summary>토큰이냐 크레딧이냐. 세는 단위가 다르므로 같은 말로 적으면 숫자가 엉뚱하게 읽힌다.</summary>
    private static string UnitText(string? kind) => AiUsageText.Unit(kind);

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
