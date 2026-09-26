using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsDocsBoard
{
    [Inject] private WbsDocsClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;

    private IReadOnlyList<WbsDocDto> _docs = [];
    private int? _selectedId;

    private bool _editing;
    private string? _draftTitle;
    private string? _draftContent;

    private bool _newOpen;
    private string? _newTitle;

    private ConfirmDialog? _confirm;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private WbsDocDto? Current => _docs.FirstOrDefault(d => d.Id == _selectedId);

    /// <summary>
    /// 고치던 것이 남아 있나. <b>제목과 본문을 둘 다 본다</b> — 제목만 고치고
    /// 옮기는 일이 실제로 잦다.
    /// </summary>
    private bool IsDirty =>
        _editing && Current is { } doc
        && (doc.Title != _draftTitle || (doc.Content ?? "") != (_draftContent ?? ""));

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _docs = [];
            _selectedId = null;
            return 0;
        }

        _docs = await Api.ListAsync(rid);

        // 보던 쪽이 아직 있으면 그대로 둔다. 저장할 때마다 첫 쪽으로
        // 튕기면 고친 결과를 눈으로 확인할 수 없다.
        if (!_docs.Any(d => d.Id == _selectedId))
        {
            _selectedId = _docs.FirstOrDefault()?.Id;
        }

        return _docs.Count;
    }, "문서가 없습니다.", "문서를 읽지 못했습니다");

    /// <summary>
    /// 다른 쪽으로 옮긴다. <b>고치던 것이 있으면 먼저 묻는다</b> —
    /// 안 물으면 쓰던 글이 말없이 사라진다.
    /// </summary>
    private async Task SelectAsync(int id)
    {
        if (id == _selectedId) return;

        if (IsDirty && _confirm is not null)
        {
            var ok = await _confirm.AskAsync(
                "고치던 내용이 저장되지 않았습니다. 그래도 옮길까요?",
                "저장하지 않고 옮기기", "옮긴다", ButtonRenderStyle.Secondary);

            if (!ok) return;
        }

        _editing = false;
        _selectedId = id;
    }

    private void StartEdit()
    {
        if (Current is not { } doc) return;

        _draftTitle = doc.Title;
        _draftContent = doc.Content;
        _editing = true;
    }

    private void CancelEdit() => _editing = false;

    private async Task SaveAsync()
    {
        if (ProjectRid is not int rid || Current is not { } doc) return;

        if (string.IsNullOrWhiteSpace(_draftTitle))
        {
            Say("제목은 필수입니다.", NoticeTone.Warning);
            return;
        }

        var saved = await RunAsync(
            () => Api.UpdateAsync(rid, new WbsDocDto
            {
                Id = doc.Id,
                Title = _draftTitle,
                Content = _draftContent ?? "",
                SortOrder = doc.SortOrder,
            }),
            "저장했습니다.", "저장하지 못했습니다");

        if (!saved) return;

        _editing = false;
        await SearchAsync();
    }

    private void StartNew()
    {
        _newTitle = null;
        _newOpen = true;
    }

    /// <summary>
    /// 제목만 받아 만들고 <b>바로 편집으로 들어간다.</b> 본문이 빈 쪽을
    /// 만들어 두고 나중에 찾아오게 하지 않는다.
    /// </summary>
    private async Task CreateAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_newTitle))
        {
            Say("제목을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var title = _newTitle.Trim();

        var made = await RunAsync(
            () => Api.CreateAsync(rid, new WbsDocDto
            {
                Title = title,
                Content = "",

                // 맨 뒤에 붙인다. 0 으로 두면 서버가 100 을 주는데, 이미
                // 100 짜리가 있으면 차례가 겹쳐 목차 순서가 흔들린다.
                SortOrder = _docs.Count == 0 ? 100 : _docs.Max(d => d.SortOrder) + 10,
            }),
            "만들었습니다.", "만들지 못했습니다");

        if (!made) return;

        _newOpen = false;
        await SearchAsync();

        var created = _docs.FirstOrDefault(d => d.Title == title);

        if (created is not null)
        {
            _selectedId = created.Id;
            StartEdit();
        }
    }

    /// <summary>지운 뒤에는 <b>이웃 쪽</b>을 고른다. 빈 화면으로 떨어뜨리지 않는다.</summary>
    private async Task DeleteAsync()
    {
        if (ProjectRid is not int rid || Current is not { } doc || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"「{doc.Title}」 쪽을 지웁니다.\n되돌릴 수 없습니다.", "쪽 삭제");

        if (!ok) return;

        var index = _docs.ToList().FindIndex(d => d.Id == doc.Id);
        var next = _docs.ElementAtOrDefault(index + 1) ?? _docs.ElementAtOrDefault(index - 1);

        var gone = await RunAsync(
            () => Api.DeleteAsync(rid, doc.Id), "지웠습니다.", "지우지 못했습니다");

        if (!gone) return;

        _editing = false;
        _selectedId = next?.Id;

        await SearchAsync();
    }
}
