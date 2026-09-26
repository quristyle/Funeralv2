using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class CompanySearchPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery(Name = "q")]
    public string? Q { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "field")]
    public string? Field { get; set; }

    /// <summary>검색 기준. 값은 서버의 <c>field</c> 그대로다.</summary>
    private static readonly IReadOnlyList<SchOption> Fields =
    [
        new("all", SchSummary.Any),
        new("name", "회사명"),
        new("bizno", "사업자번호"),
        new("ceo", "대표자"),
        new("phone", "전화"),
        new("address", "주소"),
    ];

    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(Fields, f => f.Value, f => f.Text, _field),
        _q);

    private string? _field = "all";
    private string? _q;

    /// <summary>마지막으로 찾은 검색어. <c>null</c> 이면 아직 찾지 않았다.</summary>
    private string? _searched;

    private IReadOnlyList<CompanySummary> _results = [];
    private CompanyCreatePopup? _create;

    /// <summary>주소에서 읽은 마지막 조건. 같은 주소로 다시 그려질 때 또 부르지 않으려고 든다.</summary>
    private (string? Q, string? Field)? _loaded;

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded == (Q, Field))
        {
            return;
        }

        _loaded = (Q, Field);
        _q = Q;
        _field = Fields.Any(f => f.Value == Field) ? Field : "all";

        // 검색어가 없어도 부른다 — 서버는 그것을 「전체」로 읽는다. 다만 사업자번호
        // 기준은 열 자리 전체를 요구해서 빈 검색어로 부르면 400 이다.
        if (_field != "bizno" || !string.IsNullOrWhiteSpace(Q))
        {
            await LoadResultsAsync();
        }
    }

    private async Task OnKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAsync();
        }
    }

    /// <summary>
    /// 조건을 주소에 싣고 옮긴다. 같은 주소면 옮겨도 파라미터가 안 바뀌어 다시 부르지
    /// 않으므로 그때만 직접 부른다(「같은 검색어로 다시 찾기」).
    /// </summary>
    private async Task SearchAsync()
    {
        var q = _q?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(q) && _field == "bizno")
        {
            Say("사업자번호를 입력하십시오.", NoticeTone.Warning);
            return;
        }

        if (q == (Q ?? string.Empty) && _field == (Field ?? "all"))
        {
            await LoadResultsAsync();
            return;
        }

        Navigation.NavigateTo(
            $"/cargotrust/companies?q={Uri.EscapeDataString(q)}&field={Uri.EscapeDataString(_field ?? "all")}");
    }

    private Task LoadResultsAsync() => LoadAsync(async () =>
    {
        var q = (_q ?? string.Empty).Trim();
        _results = await Api.SearchCompaniesAsync(q, _field);
        _searched = q;

        // 「없습니다」는 안내 줄이 이미 말한다 — 토스트까지 띄우면 같은 말이 두 번이다.
        return -1;
    }, failMessage: "거래처를 찾지 못했습니다");
}
