using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class UseCaseView
{
    [Inject] private ProjectPropClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _name);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>도구상자에 놓을 도형 목록. **JS 가 정본**이라 거기서 받아 온다.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> _shapes = [];

    /// <summary>도구상자의 「선」 칸.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> _edgeStyles = [];

    /// <summary>도구상자가 보이는가. 숨기면 손잡이만 남는다.</summary>
    private bool _toolsOpen = true;

    /// <summary>
    /// 핀이 꽂혀 있는가. <b>꽂히면 그림 옆에 자리를 차지하고</b>, 빼면 그림
    /// 위에 뜬다. 기본은 꽂힘 — 떠 있으면 그 자리에 도형을 놓을 수 없다.
    /// </summary>
    private bool _toolsPinned = true;

    /// <summary>
    /// 겹친 것만 밀어내 고루 펼친다. <b>배치를 새로 짜지 않는다</b> —
    /// 사람이 놓아 둔 자리는 그대로 둔다.
    /// </summary>
    private async Task SpreadAsync()
    {
        if (_diagram is null) return;

        var moved = await _diagram.SpreadAsync();

        if (moved > 0)
        {
            _dirty = true;
        }
    }

    /// <summary>
    /// 보기 좋게 펼친다. <b>관계선이 없으면 격자로 떨어진다</b> — 그 사실을
    /// 말해 주지 않으면 「눌렀는데 아무 일도 없다」로 보인다.
    /// </summary>
    private async Task ArrangeAsync(string kind)
    {
        if (_diagram is null) return;

        var used = await _diagram.ArrangeAsync(kind);
        _dirty = true;

        if (used == "grid" && kind != "grid")
        {
            Say("관계선이 없어 격자로 놓았습니다. 계층·유기 배치는 이어진 선이 있어야 합니다.",
                NoticeTone.Info);
        }
    }

    private async Task AddShapeAsync(string kind)
    {
        if (_diagram is null) return;

        await _diagram.AddEntityAsync(kind: kind);
    }

    /// <summary>고른 선의 모양을 바꾼다. 선을 안 골랐으면 그렇게 말해 준다.</summary>
    private async Task StyleEdgeAsync(string kind)
    {
        if (_diagram is null) return;

        var changed = await _diagram.StyleEdgeAsync(kind);

        if (changed == 0)
        {
            Say("먼저 캔버스에서 선을 고르십시오.", NoticeTone.Info);
            return;
        }

        _dirty = true;
    }

    /// <summary>
    /// 도구상자 목록을 받아 둔다. <b>그림 부품이 생긴 뒤에만</b> 물을 수 있다 —
    /// 그 목록은 JS 모듈 안에 있고, 모듈은 부품이 처음 그려질 때 실린다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _diagram is null || _shapes.Count > 0)
        {
            return;
        }

        _shapes = await _diagram.ShapesAsync();
        _edgeStyles = await _diagram.EdgeStylesAsync();
        StateHasChanged();
    }

    /// <summary>이 화면이 다루는 속성 갈래. 열쇠의 한 칸이다.</summary>
    private const string PropType = "USE_CASE";

    private DiagramViewer? _diagram;

    /// <summary>
    /// 캔버스를 고쳤는데 아직 저장 안 했다. 그림은 저장 단추를 눌렀을 때만
    /// 모델로 되돌아가므로, 그 사실을 화면이 말해 주지 않으면 사람이 배치를
    /// 잃는다.
    /// </summary>
    private bool _dirty;

    /// <summary>
    /// 저장본이 <b>옛 도구(draw.io)의 XML</b> 인가. 그러면 저장을 막는다 —
    /// 여기서 만든 JSON 으로 덮어쓰면 그 그림은 되돌릴 수 없다.
    /// </summary>
    private bool _legacy;

    private string? _projectCode;
    private string? _name;

    /// <summary>고른 프로젝트에 저장된 그림 이름들.</summary>
    private List<string> _names = [];

    private Task ZoomInAsync() => _diagram?.ZoomInAsync() ?? Task.CompletedTask;
    private Task DeleteSelectionAsync() => _diagram?.DeleteSelectionAsync() ?? Task.CompletedTask;
    private Task ZoomOutAsync() => _diagram?.ZoomOutAsync() ?? Task.CompletedTask;
    private Task FitAsync() => _diagram?.FitAsync() ?? Task.CompletedTask;

    /// <summary>
    /// 이름 목록을 채운다. <b>그림은 아직 안 읽는다</b> — 어느 것을 열지는
    /// 사람이 고른다(첫 것을 열어 두면 큰 그림에서 기다림이 길다).
    /// </summary>
    private Task LoadNamesAsync()
    {
        _name = null;
        _legacy = false;

        if (string.IsNullOrWhiteSpace(_projectCode))
        {
            _names = [];
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var props = await Api.ListAsync(_projectCode, propType: PropType);

            _names = [.. props.Select(p => p.PropCd ?? string.Empty).Where(n => n.Length > 0)];
            _name = _names.FirstOrDefault();

            return _names.Count;
        }, "이 프로젝트에 저장된 유즈케이스가 없습니다. 이름을 적고 그려서 저장하십시오.",
           "유즈케이스 목록을 읽지 못했습니다");
    }

    /// <summary>이름을 바꾸면 저장 막음을 푼다 — 다른 그림은 다른 형식일 수 있다.</summary>
    private Task PickNameAsync(string? name)
    {
        _name = name;
        _legacy = false;

        return Task.CompletedTask;
    }

    private Task LoadDiagramAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectCode) || string.IsNullOrWhiteSpace(_name))
        {
            Say("프로젝트와 그림 이름을 고르십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var saved = await Api.ListAsync(_projectCode, _name, PropType);
            var raw = saved.FirstOrDefault()?.PropVal;

            // 옛 도구(draw.io)의 XML 이면 파서가 **조용히 빈 그림**을 준다.
            // 그것을 「아직 안 그렸다」로 읽은 사람이 새로 그려 저장하면 옛
            // 그림이 사라진다.
            //
            // **운영 자료 20건은 2026-09-13 에 옮겼다**(머리말). 지금 이
            // 갈래로 들어올 것은 누군가 XML 을 다시 붙여 넣은 경우뿐이다.
            _legacy = ErdModel.IsLegacyDrawing(raw);

            if (_legacy)
            {
                Say("옛 도구(draw.io)로 그린 그림입니다. 여기서는 열지 못하고, 덮어쓰지 않도록 저장을 막았습니다. 옮기려면 scripts/projmng-drawio-to-diagram.py 를 돌리고, 새로 그리려면 다른 이름으로 만드십시오.",
                    NoticeTone.Warning);

                return 1;
            }

            var model = ErdModel.Parse(raw ?? string.Empty);

            if (_diagram is not null)
            {
                await _diagram.LoadAsync(model);

                // 방금 읽은 그림이 곧 저장본이다.
                _dirty = false;
            }

            return model.Entities.Count;
        }, "저장된 그림이 없습니다. 새로 그려 저장하십시오.", "유즈케이스를 읽지 못했습니다");
    }

    private async Task SaveDiagramAsync()
    {
        if (_diagram is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_projectCode) || string.IsNullOrWhiteSpace(_name))
        {
            Say("프로젝트와 그림 이름을 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (_legacy)
        {
            Say("옛 도구로 그린 그림이라 덮어쓸 수 없습니다. 다른 이름으로 저장하십시오.", NoticeTone.Warning);
            return;
        }

        var model = await _diagram.SaveAsync();

        var saved = await RunAsync(
            () => Api.SaveAsync(new ProjectPropDto
            {
                PrjRid = _projectCode,
                PropCd = _name,
                PropType = PropType,
                PropVal = model.ToJson(),
            }),
            "저장했습니다.", "저장하지 못했습니다");

        if (saved)
        {
            _dirty = false;

            // 새 이름으로 저장했으면 고르개에도 올라와야 한다.
            if (!_names.Contains(_name, StringComparer.Ordinal))
            {
                _names = [.. _names, _name];
            }
        }
    }
}
