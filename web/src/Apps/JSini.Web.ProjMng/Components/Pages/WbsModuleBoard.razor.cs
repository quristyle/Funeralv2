using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsModuleBoard
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

    /// <summary>막대 안에 겹쳐 볼 잣대. 원본 기본값이 개발완료다.</summary>
    private string _overlay = "done_real";

    private static readonly SchOption[] OverlayOptions =
    [
        new("done", "완료 기준"),
        new("done_real", "개발완료 기준"),
        new("act_edt", "실적종료 입력 기준"),
    ];

    private IReadOnlyList<WbsBoardModuleDto> _rows = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string OverlayLabel => _overlay switch
    {
        "done" => "완료",
        "act_edt" => "실적종료 입력",
        _ => "개발완료",
    };

    private IReadOnlyList<WbsBarItem> Items =>
    [
        .. _rows.Where(r => r.Systemcode is not null).Select(r => new WbsBarItem(
            r.Systemcode!, r.Systemcode!, r.Cnt,
            _overlay switch { "done" => r.Done, "act_edt" => r.ActEdt, _ => r.DoneReal },
            $"{r.SystemNm} · 완료 {r.Done} · 개발완료 {r.DoneReal} · 실적종료 {r.ActEdt}"))
    ];

    private static string Pct(int part, int whole) =>
        whole == 0 ? "0%" : $"{Math.Round(part * 1000.0 / whole) / 10:0.#}%";

    private static string? Blank(int v) => v == 0 ? null : v.ToString();

    private static string? Dim(int v) => v == 0 ? "is-dim" : null;

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _rows = [];
            return 0;
        }

        _rows = await Api.ByModuleAsync(rid, _basis, _scope);
        return _rows.Count;
    }, "모듈별 자료가 없습니다.", "모듈별 현황을 읽지 못했습니다");

    private void Drill(string? module)
    {
        if (string.IsNullOrWhiteSpace(module)) return;

        Navigation.NavigateTo(
            $"/projmng/wbs/rows?module={Uri.EscapeDataString(module)}&basis={_basis}&scope={_scope}");
    }
}
