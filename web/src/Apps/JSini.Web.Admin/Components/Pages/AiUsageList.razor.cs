using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class AiUsageList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(FeatureOptions, o => o.Value, o => o.Text, _feature),
        SchSummary.Or(_keyword));

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

    protected override Task OnInitializedAsync() => ReloadAsync();

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
