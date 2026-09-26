using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Http;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestMonitor
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;
    private DataTable _admins = JsonTable.Empty;
    private DataTable _companies = JsonTable.Empty;

    private static readonly Dictionary<string, string> AdminCaptions = new(StringComparer.Ordinal)
    {
        ["adminName"] = "담당자",
        ["inProgressCount"] = "진행 중",
        ["completedCount"] = "완료",
        ["consultationCount"] = "협의",
        ["negotiationCount"] = "조율",
        ["rejectedCount"] = "반려",
    };

    private static readonly Dictionary<string, string> CompanyCaptions = new(StringComparer.Ordinal)
    {
        ["companyName"] = "고객사",
        ["openCount"] = "진행 중",
        ["closedCount"] = "완료",
        ["totalCount"] = "전체",
    };

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["reqNo"] = "요청번호",
        ["title"] = "제목",
        ["companyName"] = "고객사",
        ["requesterName"] = "요청자",
        ["adminName"] = "담당자",
        ["statusName"] = "상태",
        ["createdAt"] = "접수일",
        ["completedAt"] = "완료일",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // `requests/recent` 는 없다. 최근 목록도 통계 묶음에 있고,
        // **`topN` 이 필수다** — 안 보내면 400 이 난다.
        // 셋을 나란히. **하나가 실패해도 나머지를 보여 준다** —
        // 지켜보는 화면이라 절반이라도 떠 있는 편이 낫다.
        var recent = SafeAsync(() => Api.GetAsync<JsonElement>("dashboard/requests/recent", new { topN = 50 }));
        var admins = SafeAsync(() => Api.GetAsync<JsonElement>("dashboard/all-admin-stats"));
        var companies = SafeAsync(() => Api.GetAsync<JsonElement>("dashboard/company-stats"));

        await Task.WhenAll(recent, admins, companies);

        _rows = JsonTable.From(recent.Result);
        _admins = JsonTable.From(admins.Result);
        _companies = JsonTable.From(companies.Result);

        return _rows.Rows.Count + _admins.Rows.Count;
    }, "최근 접수된 요청이 없습니다.", "요청 현황을 읽지 못했습니다");

    /// <summary>실패를 삼키고 <c>default</c> 를 돌려준다.</summary>
    private static async Task<T?> SafeAsync<T>(Func<Task<T?>> load)
    {
        try
        {
            return await load();
        }
        catch (ApiException)
        {
            return default;
        }
    }
}
