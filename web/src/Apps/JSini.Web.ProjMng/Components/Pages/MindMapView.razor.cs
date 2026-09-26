using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class MindMapView
{
    [Inject] private ProjectPropClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _title);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>이 화면이 다루는 속성 갈래. 열쇠의 한 칸이다.</summary>
    private const string PropType = "MIND_MAP";

    private MindMapCanvas? _canvas;
    private ConfirmDialog? _confirm;

    /// <summary>고를 수 있는 마디 모양. <b>JS 가 정본</b>이라 거기서 받아 온다.</summary>
    private IReadOnlyList<MindMapCanvas.ShapeOption> _shapes = [];

    private string? _projectCode;

    /// <summary>
    /// 지금 다루는 마인드맵의 제목. <b>빈 글자가 「아직 없다」</b>다 —
    /// 적는 칸(<c>DxTextBox</c>)이 널을 주지 않기 때문이다.
    /// </summary>
    private string _title = string.Empty;

    /// <summary>
    /// 고르개에서 고른 제목. <see cref="_title"/> 과 갈라 둔다 — 새 이름을
    /// 적는 순간 고르개의 값은 목록에 없는 것이 되고, 그때 고르개에 그 값을
    /// 밀어 넣으면 DevExpress 가 지워 버린다(위 칸 주석).
    /// </summary>
    private string? _picked;

    /// <summary>고른 프로젝트에 저장된 마인드맵 제목들.</summary>
    private List<string> _titles = [];

    /// <summary>
    /// 캔버스를 고쳤는데 아직 저장 안 했다. 그림은 저장 단추를 눌렀을 때만
    /// 서버로 가므로, 그 사실을 화면이 말해 주지 않으면 사람이 잃는다.
    /// </summary>
    private bool _dirty;

    /// <summary>
    /// 지금 보고 있는 것이 <b>서버에 있는 마인드맵</b>인가. 삭제 단추를 켜고
    /// 끄는 데 쓴다 — 아직 저장 안 한 새 그림에는 지울 것이 없다.
    /// </summary>
    private bool _saved;

    /// <summary>mermaid 글 칸이 펴져 있는가.</summary>
    private bool _textOpen;

    private string _mermaid = string.Empty;

    /// <summary>
    /// 모양 목록을 받아 둔다. <b>캔버스가 생긴 뒤에만</b> 물을 수 있다 —
    /// 그 목록은 JS 모듈 안에 있고, 모듈은 캔버스가 처음 그려질 때 실린다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _canvas is null || _shapes.Count > 0)
        {
            return;
        }

        _shapes = await _canvas.ShapesAsync();
        StateHasChanged();
    }

    /* ── 목록·불러오기·저장 ─────────────────────────────────── */

    /// <summary>
    /// 제목 목록을 채운다. <b>그림은 아직 안 읽는다</b> — 어느 것을 열지는
    /// 사람이 고른다.
    /// </summary>
    private Task LoadTitlesAsync()
    {
        _title = string.Empty;
        _picked = null;
        _saved = false;

        if (string.IsNullOrWhiteSpace(_projectCode))
        {
            _titles = [];
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var props = await Api.ListAsync(_projectCode, propType: PropType);

            _titles = [.. props.Select(p => p.PropCd ?? string.Empty).Where(n => n.Length > 0)];
            PickTitle(_titles.FirstOrDefault());

            return _titles.Count;
        }, "이 프로젝트에 저장된 마인드맵이 없습니다. 제목을 적고 그려서 저장하십시오.",
           "마인드맵 목록을 읽지 못했습니다");
    }

    /// <summary>
    /// 고르개에서 고른 제목을 적는 칸으로 옮긴다. <b>두 칸이 같은 것을
    /// 가리키게 하는 유일한 자리다</b> — 반대 방향(적는 칸 → 고르개)은 가지
    /// 않는다(목록에 없는 값을 고르개에 넣으면 지워진다).
    /// </summary>
    private void PickTitle(string? name)
    {
        _picked = name;
        _title = name ?? string.Empty;
    }

    private Task LoadMapAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectCode) || string.IsNullOrWhiteSpace(_title))
        {
            Say("프로젝트를 고르고 마인드맵 제목을 적으십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var saved = await Api.ListAsync(_projectCode, _title, PropType);
            var raw = saved.FirstOrDefault()?.PropVal;

            _saved = saved.Count > 0;

            // 저장본이 없으면 뿌리 하나짜리 새 그림에서 시작한다. 빈 캔버스를
            // 주면 「무엇부터 하라는 것인가」가 되고, 마인드맵은 뿌리가
            // 있어야만 시작할 수 있는 그림이다.
            var model = _saved ? MindMapModel.Parse(raw) : NewMap();

            if (_canvas is not null)
            {
                await _canvas.LoadAsync(model);
                await RefreshTextAsync();

                // 방금 읽은 그림이 곧 저장본이다.
                _dirty = false;
            }

            return _saved ? model.Count : 0;
        }, "저장된 마인드맵이 없습니다. 새로 그려 저장하십시오.", "마인드맵을 읽지 못했습니다");
    }

    /// <summary>
    /// 새 마인드맵을 캔버스에 올린다. <b>서버에는 아무 일도 하지 않는다</b> —
    /// 제목을 적고 「저장」을 눌렀을 때 생긴다.
    /// </summary>
    private async Task NewMapAsync()
    {
        if (_canvas is null)
        {
            return;
        }

        if (_dirty && !await Ask("저장하지 않은 변경이 있습니다.\n새로 만들면 사라집니다.", "새로 만들기"))
        {
            return;
        }

        _saved = false;
        await _canvas.LoadAsync(NewMap());
        await RefreshTextAsync();

        _dirty = false;
        Say("새 마인드맵입니다. 제목을 적고 저장하십시오.");
    }

    /// <summary>뿌리 하나짜리 새 그림. 뿌리 이름은 제목을 따른다.</summary>
    private MindMapModel NewMap()
    {
        var model = MindMapModel.Empty;

        if (!string.IsNullOrWhiteSpace(_title))
        {
            model.Root.Text = _title;
        }

        return model;
    }

    private async Task SaveMapAsync()
    {
        if (_canvas is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_projectCode) || string.IsNullOrWhiteSpace(_title))
        {
            Say("프로젝트를 고르고 마인드맵 제목을 적으십시오.", NoticeTone.Warning);
            return;
        }

        var model = await _canvas.SaveAsync();

        var ok = await RunAsync(
            () => Api.SaveAsync(new ProjectPropDto
            {
                PrjRid = _projectCode,
                PropCd = _title,
                PropType = PropType,
                PropVal = model.ToJson(),

                // 설명 칸에 마디 수를 남긴다. DB 속성 화면에서 값을 펼치지
                // 않고도 빈 그림과 그린 그림을 가릴 수 있다.
                PropComm = $"마인드맵 · 마디 {model.Count}개",
                PropUseYn = "Y",
            }),
            "저장했습니다.", "저장하지 못했습니다");

        if (!ok)
        {
            return;
        }

        _dirty = false;
        _saved = true;

        // 새 제목으로 저장했으면 고르개에도 올라와야 한다.
        if (!_titles.Contains(_title, StringComparer.Ordinal))
        {
            _titles = [.. _titles, _title];
        }

        _picked = _title;
    }

    private async Task DeleteMapAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectCode) || string.IsNullOrWhiteSpace(_title))
        {
            Say("지울 마인드맵을 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (!await Ask($"마인드맵 「{_title}」 을(를) 지웁니다.\n되돌릴 수 없습니다."))
        {
            return;
        }

        var ok = await RunAsync(
            () => Api.DeleteAsync(new ProjectPropDto
            {
                PrjRid = _projectCode,
                PropCd = _title,
                PropType = PropType,
            }),
            "지웠습니다.", "지우지 못했습니다");

        if (!ok)
        {
            return;
        }

        _titles.Remove(_title);
        PickTitle(_titles.FirstOrDefault());
        _saved = false;
        _dirty = false;

        if (_canvas is not null)
        {
            await _canvas.LoadAsync(NewMap());
            await RefreshTextAsync();
        }
    }

    /* ── 마디 손보기 ────────────────────────────────────────── */

    private Task ZoomInAsync() => _canvas?.ZoomInAsync() ?? Task.CompletedTask;
    private Task ZoomOutAsync() => _canvas?.ZoomOutAsync() ?? Task.CompletedTask;
    private Task FitAsync() => _canvas?.FitAsync() ?? Task.CompletedTask;

    private Task AddChildAsync() => _canvas?.AddChildAsync() ?? Task.CompletedTask;
    private Task AddSiblingAsync() => _canvas?.AddSiblingAsync() ?? Task.CompletedTask;

    private async Task RenameAsync()
    {
        if (_canvas is null) return;

        if (!await _canvas.RenameAsync())
        {
            Say("먼저 캔버스에서 마디를 고르십시오.", NoticeTone.Info);
        }
    }

    private async Task RemoveAsync()
    {
        if (_canvas is null) return;

        // **먼저 무엇을 지우는지 알아본다.** 자식이 딸린 마디를 지우면 그
        // 아래가 통째로 사라지는데, 그 사실을 묻지 않고 지우면 되돌릴 길이 없다.
        var node = await _canvas.SelectionAsync();

        if (node is null)
        {
            Say("먼저 캔버스에서 마디를 고르십시오.", NoticeTone.Info);
            return;
        }

        if (node.IsRoot)
        {
            Say("뿌리는 지울 수 없습니다. 마인드맵 자체를 없애려면 「마인드맵 삭제」를 쓰십시오.",
                NoticeTone.Warning);
            return;
        }

        if (node.ChildCount > 0
            && !await Ask($"「{node.Text}」 과(와) 그 아래 {node.ChildCount}개 가지를 지웁니다."))
        {
            return;
        }

        await _canvas.RemoveAsync();
    }

    private async Task ToggleCollapseAsync()
    {
        if (_canvas is null) return;

        if (!await _canvas.ToggleCollapseAsync())
        {
            Say("접을 가지가 있는 마디를 고르십시오.", NoticeTone.Info);
        }
    }

    private async Task ExpandAllAsync()
    {
        if (_canvas is null) return;

        if (await _canvas.ExpandAllAsync() == 0)
        {
            Say("접힌 가지가 없습니다.", NoticeTone.Info);
        }
    }

    private async Task CollapseAllAsync()
    {
        if (_canvas is null) return;

        if (await _canvas.CollapseAllAsync() == 0)
        {
            Say("접을 가지가 없습니다.", NoticeTone.Info);
        }
    }

    private async Task SetShapeAsync(string kind)
    {
        if (_canvas is null) return;

        if (!await _canvas.SetShapeAsync(kind))
        {
            Say("먼저 캔버스에서 마디를 고르십시오.", NoticeTone.Info);
        }
    }

    /* ── mermaid 글 ─────────────────────────────────────────── */

    /// <summary>
    /// 캔버스가 바뀌었다.
    ///
    /// <para>
    /// mermaid 글은 <b>칸이 펴져 있을 때만</b> 다시 만든다. 닫혀 있는 칸을
    /// 채우려고 마디 하나 고칠 때마다 나무를 통째로 받아 오면, 안 보는 글을
    /// 위해 회로를 왕복하는 셈이다.
    /// </para>
    /// </summary>
    private async Task OnCanvasChangedAsync()
    {
        _dirty = true;

        if (_textOpen)
        {
            await RefreshTextAsync();
        }
    }

    private async Task ToggleTextAsync()
    {
        _textOpen = !_textOpen;

        if (_textOpen)
        {
            await RefreshTextAsync();
        }
    }

    /// <summary>그림을 mermaid 글로 옮겨 적는다.</summary>
    private async Task RefreshTextAsync()
    {
        if (_canvas is null) return;

        _mermaid = (await _canvas.SaveAsync()).ToMermaid();
    }

    /// <summary>
    /// 글을 읽어 그림을 다시 그린다. <b>지금 그림을 덮어쓴다</b> — 되돌릴 수
    /// 없으므로 한 번 묻는다.
    /// </summary>
    private async Task ApplyTextAsync()
    {
        if (_canvas is null) return;

        var model = MindMapModel.FromMermaid(_mermaid);

        if (!await Ask($"글에서 읽은 마디 {model.Count}개로 지금 그림을 덮어씁니다.", "가져오기"))
        {
            return;
        }

        await _canvas.LoadAsync(model);

        // 읽은 글을 그대로 두지 않는다. 사람이 적은 글과 우리가 읽어 낸 나무가
        // 어긋난 자리(못 읽은 줄·따옴표)를 **눈으로 볼 수 있어야** 한다.
        await RefreshTextAsync();

        _dirty = true;
        Say($"마디 {model.Count}개를 읽었습니다. 「저장」을 눌러야 남습니다.");
    }

    /// <summary>
    /// 한 번 묻는다. 확인창이 아직 안 그려졌으면(프리렌더) 그대로 진행한다 —
    /// 그 상태에서는 사람이 단추를 누를 수도 없다.
    /// </summary>
    private async Task<bool> Ask(string message, string confirmText = "삭제")
        => _confirm is null || await _confirm.AskAsync(message, confirmText: confirmText);
}
