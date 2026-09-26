using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class GitCiBoard
{
    [Inject] private GitStatusClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        SchSummary.NameOf(StateOptions, o => o.Value, o => o.Text, _state));

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string? _state;

    private GitResultDto<GitRunDto>? _result;
    private IReadOnlyList<GitRunDto> _rows = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private static readonly SchOption[] StateOptions =
    [
        new(null, "전체"),
        new("failure", "실패만"),
        new("success", "성공만"),
        new("error", "못 읽음"),
    ];

    private IReadOnlyList<GitRunDto> Filtered => _state switch
    {
        null => _rows,
        "error" => [.. _rows.Where(r => r.Error is not null)],
        var s => [.. _rows.Where(r => r.Error is null && r.Status == s)],
    };

    private int Count(string status) => _rows.Count(r => r.Error is null && r.Status == status);

    private string RateText =>
        _result?.RateRemaining is null ? "—" : $"{_result.RateRemaining} / {_result.RateLimit}";

    private Task SearchAsync() => LoadInternalAsync(false);

    private Task RefreshAsync() => LoadInternalAsync(true);

    private Task LoadInternalAsync(bool refresh) => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _result = null;
            _rows = [];
            return 0;
        }

        _result = await Api.CiAsync(rid, refresh);
        _rows = _result?.Rows ?? [];

        return _rows.Count;
    }, "저장소가 없습니다.", "빌드 상태를 읽지 못했습니다");
}
