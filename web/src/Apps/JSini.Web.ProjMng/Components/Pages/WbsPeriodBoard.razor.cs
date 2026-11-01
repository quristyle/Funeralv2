using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsPeriodBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope));

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private string? _projectCode;
    private string _basis = "edt";
    private string _scope = "dev";

    private IReadOnlyList<WbsBarItem> MonthItems { get; set; } = [];
    private IReadOnlyList<WbsBarItem> WeekItems { get; set; } = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            MonthItems = WeekItems = [];
            return 0;
        }

        var monthly = Api.MonthlyAsync(rid, _basis, _scope);
        var weekly = Api.WeeklyAsync(rid, _basis, _scope);

        await Task.WhenAll(monthly, weekly);

        MonthItems =
        [
            .. monthly.Result.Where(r => r.Bucket is not null).Select(r => new WbsBarItem(
                r.Bucket!, r.Bucket!, r.Cnt, r.Done,
                $"{r.Cnt}건 · 완료 {r.Done} · 담당자 {r.Users}명"))
        ];

        // 주 이름 옆에 시작일(월/일)을 붙인다 — 「2026-W33」만으로는 어느 주인지 바로 안 떠오른다.
        WeekItems =
        [
            .. weekly.Result.Where(r => r.Bucket is not null).Select(r => new WbsBarItem(
                r.Bucket!,
                r.WeekStart is { Length: >= 10 } ws ? $"{r.Bucket} ({ws[5..].Replace('-', '/')})" : r.Bucket!,
                r.Cnt, r.Done,
                $"{r.WeekStart} ~ {r.WeekEnd} · 완료 {r.Done}"))
        ];

        return MonthItems.Count;
    }, "기간별 자료가 없습니다.", "기간별 현황을 읽지 못했습니다");

    private void Drill(string period, string bucket) =>
        Navigation.NavigateTo(
            $"/projmng/wbs/rows?{period}={Uri.EscapeDataString(bucket)}&basis={_basis}&scope={_scope}");
}
