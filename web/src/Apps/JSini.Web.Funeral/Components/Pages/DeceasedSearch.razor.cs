using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class DeceasedSearch
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _keyword,
        SchSummary.Period(_from, _to));

    private string? _buildingId;
    private string? _roomId;
    private string? _keyword;
    private DateTime? _from;
    private DateTime? _to;

    private IReadOnlyList<DeceasedLookup> _rows = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.SearchDeceasedAsync(_buildingId, _roomId, _keyword, null, _from, _to);
        return _rows.Count;
    }, "조건에 맞는 고인 자료가 없습니다.", "고인 정보를 읽지 못했습니다");
}
