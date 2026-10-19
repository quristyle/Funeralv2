using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class FlowView
{
    [Inject] private ProjectDbClient Dbs { get; set; } = default!;
    [Inject] private ProjMngClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _dbItem?.Name, _prop?.Label);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    /// <summary>도구상자에 놓을 도형 목록. **JS 가 정본**이라 거기서 받아 온다.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> _shapes = [];

    /// <summary>도구상자의 「선」 칸.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> _edgeStyles = [];

    /// <summary>도구상자가 보이는가. 숨기면 손잡이만 남는다.</summary>
    private bool _toolsOpen = true;

    /// <summary>핀이 꽂혀 있는가. 꽂히면 그림 옆에 자리를 차지하고, 빼면 그림 위에 뜬다. 기본은 꽂힘 — 떠 있으면 도형을 놓을 수 없다.</summary>
    private bool _toolsPinned = true;

    /// <summary>겹친 것만 밀어내 고루 펼친다. 배치를 새로 짜지 않는다.</summary>
    private async Task SpreadAsync()
    {
        if (_diagram is null) return;

        var moved = await _diagram.SpreadAsync();

        if (moved > 0)
        {
            _dirty = true;
        }
    }

    /// <summary>보기 좋게 펼친다. 관계선이 없으면 격자로 떨어지므로 그 사실을 알린다.</summary>
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

    /// <summary>도구상자 목록을 받아 둔다. 그림 부품이 생긴 뒤에만 물을 수 있다(목록은 JS 모듈 안에 있다).</summary>
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

    /// <summary>그림을 담는 DB 속성의 이름. ERD 화면과 같은 배치를 나눠 쓴다.</summary>
    private const string PropKey = "erd";

    /// <summary>지금 열어 둔 것이 그 DB 의 흐름도인가(db_pkey = 'erd'). ERD 화면과 같은 판단이다.</summary>
    private bool Canonical =>
        _prop is null
        || string.Equals(_prop.Prop.DbPkey, PropKey, StringComparison.OrdinalIgnoreCase);

    private DiagramViewer? _diagram;

    /// <summary>캔버스를 고쳤는데 아직 저장 안 했다. 그림은 저장 때만 모델로 되돌아가므로, 화면이 알려 주지 않으면 배치를 잃는다.</summary>
    private bool _dirty;
    private string? _projectCode;
    private string? _dbCode;

    /// <summary>고른 DB 항목 전체. 접속 별칭·종류가 여기 들어 있다.</summary>
    private CommonCodeItem? _dbItem;

    /// <summary>저장본 줄. 있으면 고치고 없으면 새로 넣는다.</summary>
    /// <summary>지금 열어 둔 그림. 고르개의 값이자 저장이 쓸 줄이다.</summary>
    private DrawingOption? _prop;

    /// <summary>이 접속에 저장된 그림들. 조회할 때 함께 읽는다.</summary>
    private IReadOnlyList<DrawingOption> _drawings = [];

    /// <summary>그림을 갈아 끼운다. 조회를 다시 부른다 — 저장본만 갈면 대상 DB 의 표 목록이 빠져 그림이 완성되지 않는다.</summary>
    private Task PickDrawingAsync(DrawingOption? drawing)
    {
        _prop = drawing;

        return drawing is null ? Task.CompletedTask : LoadDiagramAsync();
    }

    /// <summary>저장본이 옛 도구(mxGraph)의 것인가. 그렇다면 그리지도 고치지도 못하며 저장을 막는 표시다 — 덮어쓰면 사람이 그린 그림이 사라진다.</summary>
    private bool _legacyDrawing;

    private Task ZoomInAsync() => _diagram?.ZoomInAsync() ?? Task.CompletedTask;
    private Task DeleteSelectionAsync() => _diagram?.DeleteSelectionAsync() ?? Task.CompletedTask;
    private Task ZoomOutAsync() => _diagram?.ZoomOutAsync() ?? Task.CompletedTask;
    private Task FitAsync() => _diagram?.FitAsync() ?? Task.CompletedTask;

    private Task LoadDiagramAsync()
    {
        if (string.IsNullOrWhiteSpace(_dbCode))
        {
            Say("DB 를 먼저 고르십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            // ① 저장본은 프로시저를 부르지 않고 읽는다.
            _drawings = DrawingOption.From(await Dbs.PropsAsync(DbRid));
            _prop = DrawingOption.Resume(_drawings, _prop, PropKey);

            var raw = _prop?.Prop.DbPvalue ?? string.Empty;
            _legacyDrawing = ErdModel.IsLegacyDrawing(raw);

            if (_legacyDrawing)
            {
                // 파서에 넣으면 조용히 빈 그림이 된다. 그것을 「아직 안 그렸다」로 읽고 새로 그려 저장하면 옛 그림이 사라진다.
                Say("옛 도구(draw.io)로 그린 그림입니다. 여기서는 열지 못하고, 덮어쓰지 않도록 저장을 막았습니다.",
                    NoticeTone.Warning);

                return 1;
            }

            var model = ErdModel.Parse(raw);

            // 그 DB 의 ERD 가 아니면 표를 얹지 않는다 — 다른 그림에 표 상자를 얹으면 파묻히고, 저장 때 그 표들이 눌러앉는다. 「사라진 표」 표시도 표를 그린 그림에서만 뜻이 있다.
            if (!Canonical)
            {
                if (_diagram is not null)
                {
                    await _diagram.LoadAsync(model);
                    _dirty = false;
                }

                return model.Entities.Count;
            }

            // ② 대상 DB 의 실제 테이블 목록. 이것이 있어야 사라진 것을 알아본다.
            var tables = await Client.JsContAsync(
                "tablelist",
                new Dictionary<string, object?>
                {
                    // 셋이 다 있어야 한다 — 서버가 db(종류)로 등록된 질의를 고르고 dbnick(별칭)으로 접속한다.
                    ["db"] = _dbItem?.Others.GetValueOrDefault("db_type") ?? string.Empty,
                    ["dbnick"] = _dbItem?.Others.GetValueOrDefault("db_nick") ?? string.Empty,
                    ["db_rid"] = _dbCode ?? string.Empty,
                });

            // 목록을 못 읽었으면 그렇게 말한다 — 표가 없는 것과 대상 DB 에 못 붙은 것은 고칠 곳이 다르다.
            if (tables.ProcCode < 0)
            {
                if (model.Entities.Count == 0)
                {
                    throw new ApiException(tables.Message ?? "테이블 목록을 읽지 못했습니다.");
                }

                Say($"저장된 그림만 그렸습니다. 대상 DB 의 테이블 목록을 읽지 못했습니다 — {tables.Message}",
                    NoticeTone.Warning);
            }
            else
            {
                Merge(model, tables);
            }

            if (_diagram is not null)
            {
                await _diagram.LoadAsync(model);

                // 방금 읽은 그림이 곧 저장본이다.
                _dirty = false;
            }

            return model.Entities.Count;
        }, "저장된 그림이 없습니다. 새로 그려 저장하십시오.", "업무 흐름을 읽지 못했습니다");
    }

    /// <summary>저장본과 실제 테이블 목록을 합친다. 배치는 저장본 값을 그대로 두고, DB 에서 사라진 것은 지우지 않고 표시만 남긴다(목록이 잠깐 실패해 그림이 통째로 비는 것보다 낫다).</summary>
    private static void Merge(ErdModel model, ProjMngResult tables)
    {
        var live = new HashSet<string>(StringComparer.Ordinal);
        var known = model.Entities.ToDictionary(e => e.Id, StringComparer.Ordinal);

        foreach (var row in tables.Rows ?? [])
        {
            var name = Pick(row, "TableName", "tablename");
            if (string.IsNullOrWhiteSpace(name)) continue;

            live.Add(name);

            var desc = Pick(row, "Description", "description");

            if (known.TryGetValue(name, out var exist))
            {
                model.Entities[model.Entities.IndexOf(exist)] = exist with { Desc = desc };
            }
            else
            {
                model.Entities.Add(new ErdEntity { Id = name, Name = name, Desc = desc });
            }
        }

        // 목록을 못 읽었으면 아무것도 표시하지 않는다 — 전부 (삭제됨) 이 붙으면 그림이 못 쓰게 되고 저장까지 되면 되돌리기 어렵다.
        if (live.Count == 0)
        {
            return;
        }

        for (var i = 0; i < model.Entities.Count; i++)
        {
            var entity = model.Entities[i];
            var desc = entity.Desc ?? string.Empty;

            // 손으로 만든 도형은 애초에 표가 아니다 — 표 목록에 없다고 「사라졌다」고 하면 그림이 늘 절반이 흐리게 보인다.
            if (entity.Manual || live.Contains(entity.Id))
            {
                continue;
            }

            // 이미 붙어 있으면 글자는 그대로 두고 표시만 켠다.
            if (desc.Contains("(삭제됨)", StringComparison.Ordinal))
            {
                model.Entities[i] = entity with { Gone = true };
                continue;
            }

            // 글자와 함께 흐린 도형으로도 알린다 — 도형이 많으면 글자만으로는 눈에 안 띈다.
            model.Entities[i] = entity with { Desc = $"{desc}(삭제됨)", Gone = true };
        }
    }

    /// <summary>저장본에서 사라졌다는 표시를 떼어 낸다. ERD 화면이 같은 배치를 읽으므로 남겨 두면 그쪽 그림에 (삭제됨) 이 옮아 간다.</summary>
    private static ErdModel Clean(ErdModel model) => model with
    {
        Entities =
        [
            .. model.Entities.Select(e => e with
            {
                Gone = false,
                Desc = e.Desc?.Replace("(삭제됨)", string.Empty, StringComparison.Ordinal),
            })
        ],
    };

    private async Task SaveDiagramAsync()
    {
        if (_diagram is null) return;

        // DB 를 안 골랐으면 저장할 곳이 없다 — 접속 번호 0 으로 내보내면 임자 없는 줄이 생긴다. Ctrl+S 로 아무 때나 눌리므로 불러오기와 같은 말로 막는다.
        if (string.IsNullOrWhiteSpace(_dbCode))
        {
            Say("DB 를 먼저 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (_legacyDrawing)
        {
            Say("옛 도구로 그린 그림이라 덮어쓸 수 없습니다.", NoticeTone.Warning);
            return;
        }

        // 사라졌다는 표시는 조회 결과이지 그림의 성질이 아니다.
        var model = Clean(await _diagram.SaveAsync());

        var saved = await RunAsync(async () =>
        {
            // 처음 저장하는 접속이면 줄이 없다. 그때는 새로 넣는다.
            if (_prop is null)
            {
                var made = await Dbs.CreatePropAsync(DbRid, new ProjectDbPropDto
                {
                    DbRid = DbRid,
                    DbPkey = PropKey,
                    DbPvalue = model.ToJson(),
                    DbPtype = "diagram",
                    DbPcomment = "ERD·흐름도 배치",   // 두 화면이 같은 줄을 쓴다
                });

                // 서버가 못 만들었으면 들고 있을 것이 없다.
                if (made is null)
                {
                    return;
                }

                _prop = new DrawingOption(made, made.DbPkey ?? PropKey);
                _drawings = [.. _drawings, _prop];

                return;
            }

            // 고른 그림에 쓴다 — erd 로 못박으면 다른 이름을 열어 놓고 저장할 때 엉뚱한 줄이 덮인다.
            _prop.Prop.DbPvalue = model.ToJson();

            if (await Dbs.UpdatePropAsync(DbRid, _prop.Prop) is { } updated)
            {
                _prop = _prop with { Prop = updated };
            }
        }, "저장했습니다.", "저장하지 못했습니다");

        if (saved)
        {
            _dirty = false;
        }
        else
        {
            // 저장이 깨졌으면 들고 있던 줄을 믿지 않는다 — 없는 줄을 고치려 들면 조용히 아무 일도 안 일어난다.
            _prop = null;
        }
    }

    /// <summary>고른 DB 접속 번호.</summary>
    private int DbRid => int.TryParse(_dbCode, out var rid) ? rid : 0;

    private static string Pick(ProjMngRow? row, params string[] names)
    {
        if (row is null)
        {
            return string.Empty;
        }

        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && value is not null)
            {
                var text = value.ToString();

                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }
}
