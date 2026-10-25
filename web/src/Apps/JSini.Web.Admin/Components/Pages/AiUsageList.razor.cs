using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class AiUsageList : IDisposable
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(FeatureOptions, o => o.Value, o => o.Text, _feature),
        SchSummary.Or(_keyword));

    /// <summary>떠날 때 쓰던 모습을 맡기는 곳. 돌아오면 그대로 되찾는다.</summary>
    [Inject] private ScreenState Screen { get; set; } = default!;

    private IReadOnlyList<AiUsageByUserDto> _all = [];

    private AiUsageByUserDto? _picked;

    private DateTime? _from = AppTime.TodayDate.AddDays(-30);
    private DateTime? _to = AppTime.TodayDate;
    private string? _feature;
    private string? _keyword;

    /// <summary>고를 수 있는 기능. 값은 서버가 적는 글자 그대로다(AiFeature) — 한 글자만 달라도 조용한 빈 목록이 된다.</summary>
    private static readonly SchOption[] FeatureOptions =
    [
        new("chat-stream", "AI쳇"),
        new("chat", "AI쳇(한 번에)"),
        new("suggest-code", "공통코드 추천"),
        new("suggest-i18n", "번역 추천"),
        new("suggest-menu", "메뉴 자동 채우기"),
        new("summarize", "내용 요약"),
        new("translate", "로그 번역"),
    ];

    /// <summary>거르기는 브라우저 쪽에서 한다 — 서버에 사용자 검색 조건이 없고 사람 수가 수십이라 전부 받아 걸러도 가볍다. 기간·기능은 줄이 수천이라 서버가 좁힌다.</summary>
    private IReadOnlyList<AiUsageByUserDto> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();

            return [.. _all.Where(r =>
                (r.UserId?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.UserName?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    // ── 타일 넷 ─────────────────────────────────────────────
    // 거른 뒤의 값을 센다 — 사용자를 검색해 좁혀 놓고 타일만 전체를 가리키면 보고 있는 표와 숫자가 맞지 않는다.

    private int TotalCalls => Shown.Sum(r => r.Calls);

    private long TotalTokens => Shown.Sum(r => r.TotalTokens);

    private int TotalFailed => Shown.Sum(r => r.FailedCalls);

    private int PeopleCount => Shown.Count;

    /// <summary>토큰 수를 모르는 호출 수. 0 이 아니면 합계가 실제보다 작다 — 타일이 그 사실을 한 줄로 적는다.</summary>
    private int UnknownTokenCalls => Shown.Sum(r => r.UnknownTokenCalls);

    /// <summary>타일 아래에 적을 기간. 사람이 고른 날짜 그대로다.</summary>
    private string PeriodText => SchSummary.Period(_from, _to) ?? "전체 기간";

    /// <summary>고른 줄을 들고 있는다 — 값과 알림을 같이 준다. 알림만 받으면 강조가 곧 풀린다(CommGrd 머리말). 오른쪽 클릭 창의 「최근 호출 보기」가 이 값을 쓴다.</summary>
    private void OnPicked(AiUsageByUserDto? row) => _picked = row;

    /// <summary>이 화면이 맡겨 두는 짐의 이름. 표 모습(칸 너비·정렬·구른 자리)은 CommGrd 가 따로 맡는다.</summary>
    internal const string StateKey = "admin/status/ai-usage";

    /// <summary>떠날 때 챙기는 조건과 그 조건으로 읽어 둔 줄. 돌아오면 다시 묻지 않는다.</summary>
    private sealed record Kept(
        DateTime? From, DateTime? To, string? Feature, string? Keyword,
        IReadOnlyList<AiUsageByUserDto> Rows, string? PickedUserId);

    /// <summary>돌아온 것인가. 참이면 조회를 다시 하지 않는다 — 다시 읽으면 표가 맨 위로 돌아가
    /// 「그대로」가 깨진다.</summary>
    private bool _restored;

    /// <summary>되찾기는 동기(OnInitialized)에서 한다. 비동기로 미루면 첫 await 앞에 빈 표가 한 번
    /// 그려지고, DxGrid 가 그 빈 상태로 자기 모습을 잡아 칸 너비와 정렬이 안 돌아온다.</summary>
    protected override void OnInitialized()
    {
        if (Screen.Get<Kept>(StateKey) is not { } kept) return;

        _from = kept.From;
        _to = kept.To;
        _feature = kept.Feature;
        _keyword = kept.Keyword;
        _all = kept.Rows;
        _picked = kept.PickedUserId is { } id ? kept.Rows.FirstOrDefault(r => r.UserId == id) : null;
        _restored = kept.Rows.Count > 0;
    }

    /// <summary>맡겨 둔 줄이 있으면 조회하지 않는다. <b>빈손으로 돌아왔으면 조건만 되찾고 조회한다</b> —
    /// 조회가 끝나기 전에 떠났을 수 있고, 그것을 「다 읽어 둔 것」으로 읽으면 화면이 영영 빈 표가 된다.</summary>
    protected override Task OnInitializedAsync() => _restored ? Task.CompletedTask : ReloadAsync();

    /// <summary>떠나면서 쓰던 모습을 맡긴다. 떠날 때 한 번만 담는다 — 읽어 둔 줄이 커서
    /// 글자마다 담으면 기록이 는다. 프리렌더에서도 불리며 그때는 빈 것을 맡긴다.</summary>
    public void Dispose() =>
        Screen.Set(StateKey, new Kept(_from, _to, _feature, _keyword, _all, _picked?.UserId));

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetAiUsageByUserAsync(DateOf(_from), DateOf(_to), _feature);
        return _all.Count;
    }, "이 기간에 AI 를 쓴 기록이 없습니다.", "AI 사용량을 읽지 못했습니다");

    private Task Reset()
    {
        _from = AppTime.TodayDate.AddDays(-30);
        _to = AppTime.TodayDate;
        _feature = null;
        _keyword = null;
        return ReloadAsync();
    }

    /// <summary>그 사람의 최근 호출을 건별 사용량 상세 화면으로 연다. 같은 기간·기능을 실어 보내 상세 화면의 건수와 맞춘다.</summary>
    private void OpenDetail(AiUsageByUserDto row)
    {
        _picked = row;

        var query = new List<string>();
        if (_from.HasValue) query.Add($"from={_from.Value:yyyy-MM-dd}");
        if (_to.HasValue) query.Add($"to={_to.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(_feature)) query.Add($"feature={Uri.EscapeDataString(_feature)}");
        if (!string.IsNullOrWhiteSpace(row.UserName)) query.Add($"name={Uri.EscapeDataString(row.UserName)}");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
        var userIdPart = string.IsNullOrWhiteSpace(row.UserId)
            ? "_system"
            : Uri.EscapeDataString(row.UserId);

        Navigation.NavigateTo($"/admin/status/ai-usage/detail/{userIdPart}{qs}");
    }

    /// <summary>고르개가 주는 DateTime? 을 날짜로 바꾼다. 한국 달력 날짜다 — 서버는 이 날짜로 하루의 경계를 잡고, 시각으로 보내면 자정 언저리가 어긋난다(docs/utc-time.md).</summary>
    private static DateOnly? DateOf(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    /// <summary>기능 코드를 사람이 읽을 이름으로. 모르는 코드는 그대로 보여 준다 — 서버에 기능이 늘었는데 목록을 안 고친 경우이고, 빈 칸으로 두면 그 사실이 안 보인다.</summary>
    private static string FeatureText(string feature) =>
        FeatureOptions.FirstOrDefault(o => o.Value == feature)?.Text ?? feature;
}
