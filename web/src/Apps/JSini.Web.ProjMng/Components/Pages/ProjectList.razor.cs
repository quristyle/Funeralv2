using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ProjectList
{
    [Inject] private ProjectClient Api { get; set; } = default!;
    [Inject] private CommonCodes Codes { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_keyword);

    private IReadOnlyList<ProjectDto> _all = [];
    private string? _keyword;

    /// <summary>
    /// 검색은 브라우저에서 끝낸다. 프로젝트가 열 건 남짓이라 서버로 보낼 값이 없다.
    /// </summary>
    private IReadOnlyList<ProjectDto> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();

            return [.. _all.Where(p =>
                (p.PrjName?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.PrjNick?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.PrjDesc?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.ListAsync();
        return _all.Count;
    }, "등록된 프로젝트가 없습니다.", "프로젝트 목록을 읽지 못했습니다");

    /// <summary>
    /// 새 줄의 기본값. <b>번호는 넣지 않는다</b> — 서버가 정한다.
    /// </summary>
    private void FillNew(ProjectDto p)
    {
        p.PrjSdt = DateOnly.FromDateTime(DateTime.Today);
        p.PrjType = "blazor";
    }

    private async Task SaveAsync((ProjectDto Item, bool IsNew) e)
    {
        if (e.IsNew)
        {
            await Api.CreateAsync(e.Item);
        }
        else
        {
            await Api.UpdateAsync(e.Item);
        }

        // 프로젝트가 바뀌면 다른 화면의 projlist 드롭다운도 새로 읽어야 한다.
        Codes.Clear();
    }
}
