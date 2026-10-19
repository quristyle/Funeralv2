using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ErdView
{
    [Inject] private ProjectDbClient Dbs { get; set; } = default!;
    [Inject] private ProjMngClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _dbItem?.Name, _prop?.Label);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있어 부품이 @bind-Text 로 올려 준다.</summary>
    private string? _projectName;

    /// <summary>
    /// 그림을 담는 DB 속성의 이름. 접속 하나에 ERD 하나다.
    /// </summary>
    private const string PropKey = "erd";

    /// <summary>지금 열어 둔 것이 그 DB 의 ERD 인가. 표·칸·외래키는 이 한 장에만 얹는다 — 다른 그림에 얹으면 파묻히고 저장 때 눌러앉는다. 아무것도 안 열린 첫 저장은 erd 로 만들 참이라 참이다.</summary>
    private bool Canonical =>
        _prop is null
        || string.Equals(_prop.Prop.DbPkey, PropKey, StringComparison.OrdinalIgnoreCase);

    private DiagramViewer? _diagram;

    /// <summary>캔버스를 고쳤는데 아직 저장 안 했다. 그림은 저장 때만 모델로 되돌아가므로 화면이 알려 주지 않으면 배치를 잃는다. 한 번만 알리는 것을 가르는 값이다(MarkDirty).</summary>
    private bool _dirty;

    /// <summary>저장 안 한 변경을 알린다. 한 번만 뜬다 — 정렬을 세 번 눌러도 토스트는 한 번이다(캔버스의 OnChanged 도 한 번만 올라온다).</summary>
    private void MarkDirty()
    {
        if (_dirty)
        {
            return;
        }

        _dirty = true;
        Say("저장하지 않은 변경이 있습니다. 「저장」을 눌러야 남습니다.", NoticeTone.Warning);
    }

    /// <summary>저장본이 옛 도구(draw.io)의 XML 인가. 그러면 저장을 막는다 — JSON 으로 덮으면 되돌릴 수 없다.</summary>
    private bool _legacy;

    /// <summary>상자에 칸 목록을 펼칠지. 끄면 그 조회도 안 나간다.</summary>
    private bool _showFields = true;

    /// <summary>마지막 조회에서 받아 둔 칸 목록과 외래키. 「컬럼 보기」를 껐다 켤 때 다시 쓴다 — 저장본에는 없는 조회 결과라 들고 있지 않으면 체크 한 번에 대상 DB 를 다시 물어야 한다.</summary>
    private ProjMngResult? _columns;
    private ProjMngResult? _fkeys;

    /// <summary>캔버스에 그림이 올라가 있는가. 「컬럼 보기」가 이것을 본다.</summary>
    private bool _drawn;
    private string? _projectCode;
    private string? _dbCode;

    /// <summary>고른 DB 항목 전체. 접속 별칭·종류가 여기 들어 있다.</summary>
    private CommonCodeItem? _dbItem;

    /// <summary>저장본 줄. 있으면 고치고 없으면 새로 넣는다.</summary>
    /// <summary>지금 열어 둔 그림. 고르개의 값이자 저장이 쓸 줄이다.</summary>
    private DrawingOption? _prop;

    /// <summary>이 접속에 저장된 그림들. 조회할 때 함께 읽는다.</summary>
    private IReadOnlyList<DrawingOption> _drawings = [];

    /// <summary>그림을 갈아 끼운다. 조회를 다시 부른다 — 저장본만 갈면 대상 DB 의 표·칸·외래키가 빠져 그림이 완성되지 않고, 그 얹는 규칙을 여기 따로 두면 두 곳이 갈라진다.</summary>
    private Task PickDrawingAsync(DrawingOption? drawing)
    {
        _prop = drawing;

        return drawing is null ? Task.CompletedTask : LoadDiagramAsync();
    }

    private Task ZoomInAsync() => _diagram?.ZoomInAsync() ?? Task.CompletedTask;

    /// <summary>도구상자에 놓을 도형 목록. **JS 가 정본**이라 거기서 받아 온다.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> _shapes = [];

    /// <summary>도구상자의 「선」 칸.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> _edgeStyles = [];

    /// <summary>도구상자가 보이는가. 숨기면 손잡이만 남는다.</summary>
    private bool _toolsOpen = true;

    /// <summary>핀이 꽂혀 있는가. 꽂히면 그림 옆에 자리를 차지하고, 빠지면 그림 위에 뜬다. 기본은 꽂힘 — 떠 있으면 그 자리에 도형을 놓을 수 없다.</summary>
    private bool _toolsPinned = true;

    /// <summary>겹친 것만 밀어내 고루 펼친다. 배치를 새로 짜지 않는다 — 사람이 놓아 둔 자리는 그대로다.</summary>
    private async Task SpreadAsync()
    {
        if (_diagram is null) return;

        var moved = await _diagram.SpreadAsync();

        if (moved > 0)
        {
            MarkDirty();
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

        MarkDirty();
    }

    /// <summary>도구상자 목록을 받아 둔다. 그림 부품이 생긴 뒤에만 물을 수 있다 — 목록은 JS 모듈 안에 있고 모듈은 부품이 처음 그려질 때 실린다.</summary>
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

    /// <summary>보기 좋게 펼친다. 관계선이 없으면 격자로 떨어지므로 그 사실을 알린다.</summary>
    private async Task ArrangeAsync(string kind)
    {
        if (_diagram is null) return;

        var used = await _diagram.ArrangeAsync(kind);
        MarkDirty();

        if (used == "grid" && kind != "grid")
        {
            Say("관계선이 없어 격자로 놓았습니다. 계층·유기 배치는 이어진 선이 있어야 합니다.",
                NoticeTone.Info);
        }
    }
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
            // ①②는 나란히 부른다. 화면이 고른 DB 조건으로 물으므로 저장본을 기다릴 이유가 없다.

            // ① 저장본은 프로시저를 부르지 않고 읽는다.
            var savedTask = Dbs.PropsAsync(DbRid);

            // ② 대상 DB 의 실제 테이블 목록
            var tablesTask = Client.JsContAsync("tablelist", TargetParams());

            // ③ 칸·④ 외래키는 한 번씩만 부른다 — 등록된 질의가 스키마 전체를 한 번에 준다. 둘 다 없어도 그림은 뜬다(그 DB 종류에 등록이 없을 수 있어 실패를 빈 결과로 삼킨다).
            var columnsTask = _showFields ? TryAsync("erdColumns") : Task.FromResult(ProjMngResult.Empty);
            var relationsTask = TryAsync("erdRelations");

            await Task.WhenAll(savedTask, tablesTask, columnsTask, relationsTask);

            var tables = tablesTask.Result;

            // 이 접속에 저장된 그림들을 모은다. 같은 표에 질의와 서식이 섞여 있어 ErdModel.IsDiagram 으로 그림만 고른다.
            _drawings = DrawingOption.From(savedTask.Result);
            _prop = DrawingOption.Resume(_drawings, _prop, PropKey);

            // 옛 도구(draw.io) 그림은 여기서 열지 않는다 — 파서에 넣으면 빈 그림이 되고, 그것을 새로 그려 저장하면 옛 그림이 사라진다.
            _legacy = ErdModel.IsLegacyDrawing(_prop?.Prop.DbPvalue);

            if (_legacy)
            {
                Say("옛 도구(draw.io)로 그린 그림입니다. 여기서는 열지 못하고, 덮어쓰지 않도록 저장을 막았습니다.",
                    NoticeTone.Warning);

                return 1;
            }

            var model = ErdModel.Parse(_prop?.Prop.DbPvalue ?? string.Empty);

            // 그 DB 의 ERD 가 아니면 표를 얹지 않는다 — 구성도·인터페이스 같은 다른 그림에 표 상자를 얹으면 파묻히고, 저장하면 그 표들이 눌러앉는다. 대상 DB 조회 자체는 그대로 나간다(어느 그림을 열지 알려면 네 조회가 있어야 하고, 순서를 뒤집으면 ERD 를 열 때 왕복이 늘어난다).
            if (!Canonical)
            {
                if (_diagram is not null)
                {
                    await DrawAsync(model);
                }

                return model.Entities.Count;
            }

            // 목록을 못 읽었으면 그렇게 말한다 — 빈 목록으로 두면 「그릴 테이블이 없습니다」가 되는데, 표가 없는 것과 대상 DB 에 못 붙은 것은 고칠 곳이 다르다.
            if (tables.ProcCode < 0)
            {
                if (model.Entities.Count == 0)
                {
                    throw new ApiException(tables.Message ?? "테이블 목록을 읽지 못했습니다.");
                }

                // 저장본이 있으면 그것만이라도 그린다 — 잠깐 못 읽었다고 배치해 둔 그림까지 가리면 곤란하다.
                Say($"저장된 그림만 그렸습니다. 대상 DB 의 테이블 목록을 읽지 못했습니다 — {tables.Message}",
                    NoticeTone.Warning);

                if (_diagram is not null)
                {
                    await DrawAsync(model);
                }

                return model.Entities.Count;
            }

            // ③ 합친다. 배치(x/y)는 저장본 값을 그대로 둔다 — 건드리면 사용자가 배치한 것이 흐트러진다.
            var known = model.Entities.ToDictionary(e => e.Id, StringComparer.Ordinal);

            foreach (var row in tables.Rows ?? [])
            {
                var name = Pick(row, "TableName", "tablename");
                if (string.IsNullOrWhiteSpace(name)) continue;

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

            // 「컬럼 보기」를 껐다 켤 때 다시 쓰려고 들고 있는다.
            _columns = _showFields ? columnsTask.Result : null;
            _fkeys = relationsTask.Result;

            // ⑤ 칸 목록을 상자에 얹는다.
            Fill(model, columnsTask.Result);

            // ⑥ 외래키를 선으로 얹는다. 사람이 끈 선과 섞이지 않게 표시한다.
            Link(model, relationsTask.Result);

            if (_diagram is not null)
            {
                await DrawAsync(model);
            }

            return model.Entities.Count;
        }, "그릴 테이블이 없습니다.", "ERD 를 읽지 못했습니다");
    }

    /// <summary>그림을 캔버스에 올리고 화면 상태까지 되돌린다. 배율·이동·미니맵은 그림 부품이 되돌리고, 도구상자는 화면이 들고 있어 여기서 되돌린다. 값이 없는 옛 저장본은 지금 상태를 그대로 둔다.</summary>
    private async Task DrawAsync(ErdModel model)
    {
        if (_diagram is null) return;

        await _diagram.LoadAsync(model);
        _drawn = true;

        if (model.View is { } view)
        {
            _toolsOpen = view.Tools;
            _toolsPinned = view.Pinned;
        }

        // 방금 읽은 그림이 곧 저장본이다.
        _dirty = false;
    }

    /// <summary>「컬럼 보기」를 바꿨다. 저장본이 아니라 캔버스에서 받아 온 그림을 다시 올린다(불러온 뒤 옮긴 자리와 선을 지키려고). 칸·외래키는 _columns·_fkeys 에서 다시 얹고, 보던 자리(View)는 건드리지 않는다.</summary>
    private async Task ShowFieldsAsync(bool on)
    {
        _showFields = on;

        // 아직 아무것도 안 그렸으면 다음 「불러오기」에 반영된다.
        if (_diagram is null || !_drawn)
        {
            return;
        }

        var model = (await _diagram.SaveAsync()) with { View = null };

        // 표를 얹는 것은 그 DB 의 ERD 한 장뿐이다. 다른 그림은 받아 온 그대로 다시 올린다.
        if (Canonical)
        {
            if (on)
            {
                // 껐던 채로 불러왔으면 아직 받아 온 적이 없으므로 한 번 묻는다. 실패는 TryAsync 가 빈 결과로 삼킨다.
                _columns ??= await TryAsync("erdColumns");

                Fill(model, _columns);
            }

            Link(model, _fkeys ?? ProjMngResult.Empty);
        }

        await _diagram.LoadAsync(model);
    }

    /// <summary>그림 부품이 담아 준 화면 상태(배율·이동·미니맵) 옆에 도구상자를 얹는다. 둘은 임자가 달라 한 곳에서 합칠 수밖에 없다.</summary>
    private ErdModel WithView(ErdModel model) => model with
    {
        View = (model.View ?? new ErdViewport()) with
        {
            Tools = _toolsOpen,
            Pinned = _toolsPinned,
        },
    };

    private async Task SaveDiagramAsync()
    {
        if (_diagram is null) return;

        // DB 를 안 골랐으면 저장할 곳이 없다 — 접속 번호 0 으로 내보내면 서버가 막지 않고 임자 없는 줄이 생긴다. Ctrl+S 로 아무 때나 눌리므로 불러오기와 같은 말로 막는다.
        if (string.IsNullOrWhiteSpace(_dbCode))
        {
            Say("DB 를 먼저 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (_legacy)
        {
            Say("옛 도구로 그린 그림이라 덮어쓸 수 없습니다.", NoticeTone.Warning);
            return;
        }

        // 대상 DB 에서 얹은 것(칸 목록·외래키 선)은 저장하지 않는다. 보던 자리·미니맵·도구상자는 함께 저장한다(WithView).
        var model = WithView(Clean(await _diagram.SaveAsync()));

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

                // 서버가 못 만들었으면 들고 있을 것이 없다 — 다음 저장이 다시 만들기를 시도한다.
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

            // 못 고쳤으면 들고 있던 줄을 그대로 둔다 — 아래에서 실패로 받는다.
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

    /// <summary>개발 도구 통로에 실어 보낼 값. 서버는 db(종류)로 등록된 질의를 고르고 dbnick 으로 접속한다.</summary>
    private Dictionary<string, object?> TargetParams() => new()
    {
        ["db"] = _dbItem?.Others.GetValueOrDefault("db_type") ?? string.Empty,
        ["dbnick"] = _dbItem?.Others.GetValueOrDefault("db_nick") ?? string.Empty,
        ["db_rid"] = _dbCode ?? string.Empty,
    };

    /// <summary>있으면 읽고 없으면 빈 결과. 등록되지 않은 DB 종류가 있어 실패를 삼킨다 — 칸 목록 하나 때문에 그림 전체가 막히면 안 된다.</summary>
    private async Task<ProjMngResult> TryAsync(string action)
    {
        try
        {
            var result = await Client.JsContAsync(action, TargetParams());
            return result.ProcCode < 0 ? ProjMngResult.Empty : result;
        }
        catch (ApiException)
        {
            return ProjMngResult.Empty;
        }
    }

    /// <summary>칸 목록을 상자에 얹는다.</summary>
    private static void Fill(ErdModel model, ProjMngResult columns)
    {
        var rows = columns.Rows ?? [];
        if (rows.Count == 0) return;

        var byTable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var table = Pick(row, "TableName", "tablename");
            var column = Pick(row, "ColumnName", "columnname");
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column)) continue;

            var pk = string.Equals(Pick(row, "IsPk", "ispk"), "Y", StringComparison.OrdinalIgnoreCase);
            var type = Pick(row, "TypeNm", "typenm");

            if (!byTable.TryGetValue(table, out var list))
            {
                byTable[table] = list = [];
            }

            list.Add(pk ? $"{column} (PK) {type}" : $"{column} {type}");
        }

        for (var i = 0; i < model.Entities.Count; i++)
        {
            if (byTable.TryGetValue(model.Entities[i].Id, out var fields))
            {
                model.Entities[i] = model.Entities[i] with { Fields = fields };
            }
        }
    }

    /// <summary>외래키를 관계선으로 얹는다. 양쪽 끝이 그림에 다 있어야 긋는다 — 한쪽이 없으면 선이 허공으로 간다. 사람이 이미 같은 짝을 그어 두었으면 그쪽을 남긴다(사람이 붙인 이름이 있을 수 있다).</summary>
    private static void Link(ErdModel model, ProjMngResult relations)
    {
        var rows = relations.Rows ?? [];
        if (rows.Count == 0) return;

        var ids = model.Entities.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var drawn = model.Relations
            .Select(r => $"{r.From}\u0000{r.To}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var from = Pick(row, "FromTable", "fromtable");
            var to = Pick(row, "ToTable", "totable");

            if (!ids.Contains(from) || !ids.Contains(to)) continue;
            if (!drawn.Add($"{from}\u0000{to}")) continue;

            model.Relations.Add(new ErdRelation
            {
                From = from,
                To = to,
                Label = Pick(row, "FromColumn", "fromcolumn"),
                Auto = true,
            });
        }
    }

    /// <summary>저장본에서 대상 DB 를 읽어 얹은 것(칸 목록·외래키 선)을 뗀다. 불러올 때마다 새로 만들므로, 두지 않으면 표가 바뀐 뒤에도 옛 선이 남는다.</summary>
    private static ErdModel Clean(ErdModel model) => model with
    {
        Entities = [.. model.Entities.Select(e => e with { Fields = null })],
        Relations = [.. model.Relations.Where(r => !r.Auto)],
    };

    /// <summary>고른 DB 접속 번호.</summary>
    private int DbRid => int.TryParse(_dbCode, out var rid) ? rid : 0;

    /// <summary>프로시저마다 컬럼 대소문자가 달라 몇 이름을 차례로 본다(PostgreSQL 통로는 소문자, SQL Server 통로는 파스칼).</summary>
    private static string Pick(ProjMngRow? row, params string[] names)
    {
        if (row is null) return string.Empty;

        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}
