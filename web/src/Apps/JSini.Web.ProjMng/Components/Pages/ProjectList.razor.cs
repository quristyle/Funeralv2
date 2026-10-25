using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ProjectList
{
    [Inject] private ProjectClient Api { get; set; } = default!;
    [Inject] private RagCategoryClient RagApi { get; set; } = default!;
    [Inject] private CommonCodes Codes { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_keyword);

    private IReadOnlyList<ProjectDto> _all = [];
    private string? _keyword;

    /// <summary>고를 수 있는 RAG 카테고리. AI 서버가 멎으면 빈 목록이다.</summary>
    private IReadOnlyList<RagCategoryItem> _cates = [];

    /// <summary>검색은 브라우저에서 끝낸다. 프로젝트가 열 건 남짓이라 서버로 보낼 값이 없다.</summary>
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
        // 둘을 함께 기다린다. 카테고리 쪽은 실패를 삼키므로 목록이 비어
        // 돌아올 뿐이고, 프로젝트 목록은 그대로 뜬다.
        var load = Api.ListAsync();
        var cates = RagApi.ListAsync();

        _all = await load;
        _cates = await cates;

        FillCateText();
        return _all.Count;
    }, "등록된 프로젝트가 없습니다.", "프로젝트 목록을 읽지 못했습니다");

    /// <summary>표에 보일 카테고리 이름을 채운다. 이름을 못 찾은 열쇠는 버리지 않고 열쇠 그대로 둔다 — 지워진 카테고리를 매단 채로 있다는 것이 보여야 한다.</summary>
    private void FillCateText()
    {
        var names = _cates.ToDictionary(x => x.Id, x => x.Name, StringComparer.Ordinal);

        foreach (var p in _all)
        {
            p.RagCateText = string.Join(", ",
                (p.RagCateIds ?? []).Select(id => names.GetValueOrDefault(id, id)));
        }
    }

    /// <summary>
    /// 새 줄의 기본값. <b>번호는 넣지 않는다</b> — 서버가 정한다.
    /// </summary>
    private void FillNew(ProjectDto p)
    {
        p.PrjSdt = AppTime.Today;
        p.PrjType = "blazor";
    }

    private async Task SaveAsync((ProjectDto Item, bool IsNew) e)
    {
        // 고르개가 빈 목록으로 떴으면 사람이 손댈 수 없었던 칸이다. null 로
        // 보내 서버가 연결을 그대로 두게 한다 — 안 그러면 AI 서버가 멎은 동안
        // 프로젝트 이름만 고쳐도 매여 있던 카테고리가 전부 풀린다.
        if (_cates.Count == 0)
        {
            e.Item.RagCateIds = null;
        }

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
