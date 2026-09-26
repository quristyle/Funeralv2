using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class GitMonitorBoard
{
    [Inject] private GitStatusClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;

    private GitResultDto<GitMonitorDto>? _result;
    private IReadOnlyList<GitMonitorDto> _rows = [];
    private GitMonitorDto? _selected;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string RateText =>
        _result?.RateRemaining is null ? "—" : $"{_result.RateRemaining} / {_result.RateLimit}";

    /// <summary>합계에 잘린 값이 섞였으면 <c>+</c> 를 붙인다(화면 머리말).</summary>
    private string Commits7dText
    {
        get
        {
            var sum = _rows.Sum(r => r.Commits7d);
            return _rows.Any(r => r.Commits7dCapped) ? $"{sum}+" : sum.ToString();
        }
    }

    private string BranchHint =>
        _selected is null ? ""
        : $"전체 {_selected.Branches}개 중 {_selected.BranchesChecked}개만 날짜를 확인했습니다";

    // `switch` 의 관계 패턴(`< 1024`)을 `@code` 에 쓰지 않는다 — Razor 가 그
    // `<` 를 여는 태그로 읽고 뒤를 통째로 잘못 자른다(InterfaceList 에서 밟음).
    private static string Size(long? kb)
    {
        if (kb is null) return "—";
        if (kb < 1024) return $"{kb} KB";
        if (kb < 1024 * 1024) return $"{kb / 1024.0:0.#} MB";

        return $"{kb / 1024.0 / 1024.0:0.#} GB";
    }

    private Task SearchAsync() => LoadInternalAsync(false);

    private Task RefreshAsync() => LoadInternalAsync(true);

    private Task LoadInternalAsync(bool refresh) => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _result = null;
            _rows = [];
            _selected = null;
            return 0;
        }

        _result = await Api.MonitorAsync(rid, refresh);
        _rows = _result?.Rows ?? [];

        // 고르고 있던 저장소를 다시 잡아 준다. 새로 고칠 때마다 아래가
        // 사라지면 무엇이 바뀌었는지 볼 수 없다.
        _selected = _selected is null
            ? _rows.FirstOrDefault()
            : _rows.FirstOrDefault(r => r.Repo == _selected.Repo) ?? _rows.FirstOrDefault();

        return _rows.Count;
    }, "저장소가 없습니다.", "모니터링을 읽지 못했습니다");

    private Task OnSelectedAsync(GitMonitorDto? repo)
    {
        _selected = repo;
        return Task.CompletedTask;
    }
}
