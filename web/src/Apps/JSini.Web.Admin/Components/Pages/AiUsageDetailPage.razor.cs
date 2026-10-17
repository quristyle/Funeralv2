using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class AiUsageDetailPage
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private PortalTabs Tabs { get; set; } = default!;

    [Parameter] public string? UserId { get; set; }

    [SupplyParameterFromQuery] public string? from { get; set; }
    [SupplyParameterFromQuery] public string? to { get; set; }
    [SupplyParameterFromQuery] public string? feature { get; set; }
    [SupplyParameterFromQuery] public string? name { get; set; }

    private const int RecentCap = 200;

    private IReadOnlyList<AiUsageCallDto> _detail = [];
    private string? _loadedKey;

    private string Href => string.IsNullOrWhiteSpace(UserId)
        ? "/admin/status/ai-usage/detail"
        : $"/admin/status/ai-usage/detail/{Uri.EscapeDataString(UserId)}";

    private string Who
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return string.IsNullOrWhiteSpace(UserId) || UserId == "_system"
                    ? name
                    : $"{name} ({UserId})";
            }

            if (string.IsNullOrWhiteSpace(UserId) || UserId == "_system")
            {
                return "시스템";
            }

            return UserId;
        }
    }

    private string TabTitle => $"{Who} — 건별 사용량";

    private string DetailHeaderTitle => $"{Who} — AI 건별 사용량";

    private string ExportFileName => $"{Who}_AI_호출이력";

    private string PeriodText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
            {
                return "전체 기간";
            }
            if (string.IsNullOrWhiteSpace(from))
            {
                return $"~ {to}";
            }
            if (string.IsNullOrWhiteSpace(to))
            {
                return $"{from} ~";
            }
            return $"{from} ~ {to}";
        }
    }

    private static readonly (string Value, string Text)[] FeatureOptions =
    [
        ("chat-stream", "AI쳇"),
        ("chat", "AI쳇(한 번에)"),
        ("suggest-code", "공통코드 추천"),
        ("suggest-i18n", "번역 추천"),
        ("summarize", "내용 요약"),
        ("translate", "로그 번역"),
    ];

    private static string FeatureText(string f) =>
        FeatureOptions.FirstOrDefault(o => o.Value == f).Text is { } text ? text : f;

    protected override async Task OnParametersSetAsync()
    {
        var currentKey = $"{UserId}|{from}|{to}|{feature}|{name}";
        if (_loadedKey == currentKey)
        {
            return;
        }

        _loadedKey = currentKey;

        // 레이아웃 상단 메뉴탭에 standalone 탭으로 열기
        Tabs.Open(Href, TabTitle, standalone: true);

        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        DateOnly? fromDate = DateOnly.TryParse(from, out var f) ? f : null;
        DateOnly? toDate = DateOnly.TryParse(to, out var t) ? t : null;
        string? targetUserId = string.IsNullOrWhiteSpace(UserId) || UserId == "_system" ? null : UserId;

        _detail = await Api.GetAiUsageRecentAsync(fromDate, toDate, feature, targetUserId);

        // 데이터 로드 후 혹시 사용자 이름(UserName)이 채워져 오면 탭 제목 갱신
        if (string.IsNullOrWhiteSpace(name) && _detail.Count > 0)
        {
            var firstWithName = _detail.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.UserName));
            if (firstWithName is not null)
            {
                name = firstWithName.UserName;
                Tabs.Open(Href, TabTitle, standalone: true);
            }
        }

        return _detail.Count;
    }, string.Empty, "호출 기록을 읽지 못했습니다");

    private void ClosePage()
    {
        Tabs.Close(Href);
        Navigation.NavigateTo("/admin/status/ai-usage");
    }

    public void Dispose()
    {
    }
}
