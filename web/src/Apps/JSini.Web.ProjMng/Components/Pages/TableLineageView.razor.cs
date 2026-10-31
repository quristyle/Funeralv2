using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class TableLineageView
{
    [Inject] private ProjectDbClient Dbs { get; set; } = default!;
    [Inject] private ProjMngClient Client { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>불러온 접속. 저장은 조회줄이 아니라 이것에 한다 — 저장 안 한 채 DB 고르개를 바꿔도 엉뚱한 접속에 덮이지 않는다.</summary>
    private sealed record LoadedDb(int DbRid, string Nick, string? Schema, string? Type);

    public sealed record SeqFlow(int Value, string Text);

    private static readonly IReadOnlyList<SeqFlow> SeqFlows =
        [.. LineageGraph.SeqNames.Select((name, i) => new SeqFlow(i, $"{i + 1}. {name}"))];

    private string ConditionSummary => SchSummary.Of(_projectName, _dbItem?.Name);

    private string? _projectCode;
    private string? _projectName;
    private string? _dbCode;
    private CommonCodeItem? _dbItem;

    private ElementReference _host;
    private IJSObjectReference? _module;
    private IJSObjectReference? _instance;
    private DotNetObjectReference<TableLineageView>? _self;

    private LoadedDb? _loaded;
    private LineageSummary? _summary;

    /// <summary>저장본 줄. 있으면 고치고, 없으면 처음 저장할 때 만든다. 저장 직전에 다시 읽어 수정 시각을 견준다 — 그 사이 남이 저장했으면 덮지 않는다.</summary>
    private ProjectDbPropDto? _graphProp;
    private ProjectDbPropDto? _itemsProp;
    private ProjectDbPropDto? _seed;

    private bool _graphDirty;
    private bool _itemsDirty;
    private bool Dirty => _graphDirty || _itemsDirty;

    /// <summary>대상 DB 에서 받은 표 목록. 못 붙었으면 null 이고 그 까닭이 <see cref="_dbNotice"/> 에 있다.</summary>
    private List<LineageDbTable>? _dbTables;
    private string? _dbNotice;

    private bool _mapOn = true;
    private bool _linking;
    private LineageKind NewKind { get; set; } = LineageGraph.EdgeKinds[0];
    private bool _resetArmed;
    private bool _reloadArmed;

    // ── 거르개 ──
    private string _q = string.Empty;
    private readonly HashSet<string> _kinds = [];
    private readonly HashSet<string> _flows = [];
    private int? _lane;
    private bool _onlyLinked;
    private bool _hover = true;
    private int _foundAt;

    // ── 업무항목 ──
    private LineageItems _items = new();
    private string? _itemFilter;
    private string? _itemId;
    private string? _renaming;
    private string? _renameText;
    private string? _newItem;
    private bool _solo;
    private bool _picking;

    // ── 고른 것 ──
    private LineageNode? _selNode;
    private LineageNode _edit = new();
    private string? _editId;
    private bool _editStar;
    private LineageEdge? _selEdge;
    private LineageEdge _edgeEdit = new();

    // ── 표 놓기 ──
    private bool _pickerOpen;
    private string? _pickFilter;
    private bool _pickHidePlaced = true;
    private IReadOnlyList<LineageDbTable> _picked = [];
    private string? _manualId;
    private string? _manualKr;
    private string _pickKind = "ledger";

    private bool CanEdit => Can(MenuAction.Update);

    private LineageItem? CurItem => _items.Items.FirstOrDefault(i => i.Id == _itemId);

    private IEnumerable<LineageItem> ShownItems =>
        string.IsNullOrWhiteSpace(_itemFilter)
            ? _items.Items
            : _items.Items.Where(i => i.Name.Contains(_itemFilter.Trim(), StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<LineageTable> Found
    {
        get
        {
            var kw = _q.Trim();
            if (kw.Length == 0 || _summary is null) return [];

            return
            [
                .. _summary.Tables.Where(t =>
                    t.Id.Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || (t.Kr ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase))
            ];
        }
    }

    private bool HasFilter => _q.Length > 0 || _flows.Count > 0 || _lane is not null || _itemId is not null || _onlyLinked;

    private static readonly HashSet<string> TableKinds = ["master", "ledger", "derived", "snap"];

    private HashSet<string> PlacedIds =>
        _summary?.Tables.Select(t => t.Id.ToUpperInvariant()).ToHashSet() ?? [];

    private int Unplaced => _dbTables?.Count(t => !t.Placed) ?? 0;

    private int Missing
    {
        get
        {
            if (_dbTables is null || _summary is null) return 0;
            var db = _dbTables.Select(t => t.Name.ToUpperInvariant()).ToHashSet();
            return _summary.Tables.Count(t => TableKinds.Contains(t.Kind ?? "master") && !db.Contains(t.Id.ToUpperInvariant()));
        }
    }

    private int EmptyComments
    {
        get
        {
            if (_dbTables is null || _summary is null) return 0;
            var db = _dbTables.Where(t => !string.IsNullOrWhiteSpace(t.Description))
                .Select(t => t.Name.ToUpperInvariant()).ToHashSet();
            return _summary.Tables.Count(t => string.IsNullOrWhiteSpace(t.Kr) && db.Contains(t.Id.ToUpperInvariant()));
        }
    }

    private IEnumerable<LineageDbTable> PickRows
    {
        get
        {
            IEnumerable<LineageDbTable> rows = _dbTables ?? [];
            if (_pickHidePlaced) rows = rows.Where(t => !t.Placed);

            var kw = _pickFilter?.Trim();
            return string.IsNullOrEmpty(kw)
                ? rows
                : rows.Where(t => t.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                                  || (t.Description ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
    }

    private int PickCount => _picked.Count(t => !t.Placed) + (string.IsNullOrWhiteSpace(_manualId) ? 0 : 1);

    private static string KindText(string? kind, bool edge = false) =>
        (edge ? LineageGraph.EdgeKinds : LineageGraph.NodeKinds).FirstOrDefault(k => k.Value == kind)?.Text ?? kind ?? string.Empty;

    private string LaneName(int i) => _summary?.Lanes.FirstOrDefault(l => l.I == i)?.Name ?? (i + 1).ToString();

    // ───────────────────────── 캔버스 ─────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // RCL 경로로 부른다 — 상대 경로는 /projmng/... 기준으로 풀려 404 가 나고 회로가 끊긴다.
        _module = await Js.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/table-lineage.js");
        _self = DotNetObjectReference.Create(this);
        _instance = await _module.InvokeAsync<IJSObjectReference>("create", _host, _self);
    }

    private async Task CallAsync(string method)
    {
        if (_instance is not null) await _instance.InvokeVoidAsync(method);
    }

    private async Task ToggleMapAsync(bool on)
    {
        _mapOn = on;
        if (_instance is not null) _mapOn = await _instance.InvokeAsync<bool>("toggleMap", on);
    }

    private object FilterArgs() => new
    {
        q = _q,
        kinds = _kinds.ToArray(),
        flows = _flows.ToArray(),
        lane = _lane,
        onlyLinked = _onlyLinked,
        keep = CurItem?.Tables.ToArray(),
        solo = _solo,
        picking = _picking && CurItem is not null,
        seqOn = CurItem is not null,
        hover = _hover,
    };

    private async Task PushFilterAsync()
    {
        if (_instance is not null) await _instance.InvokeVoidAsync("setFilter", FilterArgs());
    }

    private async Task RefreshSummaryAsync()
    {
        if (_instance is null) return;
        _summary = await _instance.InvokeAsync<LineageSummary>("summary");
        MarkPlaced();
    }

    private void MarkPlaced()
    {
        if (_dbTables is null) return;
        var placed = PlacedIds;
        foreach (var t in _dbTables) t.Placed = placed.Contains(t.Name.ToUpperInvariant());
    }

    // ───────────────────────── 읽기 ─────────────────────────

    private int DbRid => int.TryParse(_dbCode, out var rid) ? rid : 0;

    private Task LoadDiagramAsync()
    {
        if (DbRid == 0)
        {
            Say("DB 를 먼저 고르십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        // 저장 안 한 것이 있으면 한 번은 막는다. 다시 읽으면 말없이 사라진다.
        if (Dirty && !_reloadArmed)
        {
            _reloadArmed = true;
            Say("저장하지 않은 것이 있습니다. 한 번 더 누르면 버리고 다시 읽습니다.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        _reloadArmed = false;
        var rid = DbRid;
        var item = _dbItem;

        return LoadAsync(async () =>
        {
            if (_instance is null) return 0;

            var propsTask = Dbs.PropsAsync(rid);
            var tablesTask = ReadDbTablesAsync(item);
            await Task.WhenAll(propsTask, tablesTask);

            var props = propsTask.Result;
            _graphProp = Latest(props, LineageGraph.GraphKey);
            _itemsProp = Latest(props, LineageGraph.ItemsKey);
            _seed = Latest(props, LineageGraph.SeedKey);
            _items = LineageGraph.ParseItems(_itemsProp?.DbPvalue);

            (_dbTables, _dbNotice) = tablesTask.Result;

            _kinds.Clear();
            _flows.Clear();
            _lane = null;
            _q = string.Empty;
            _onlyLinked = false;
            _itemId = null;
            _solo = false;
            _picking = false;
            _selNode = null;
            _selEdge = null;
            _linking = false;
            _resetArmed = false;

            await _instance.InvokeAsync<bool>("setLinking", false, NewKind.Value);
            _summary = await _instance.InvokeAsync<LineageSummary>(
                "load", _graphProp?.DbPvalue ?? LineageGraph.EmptyGraph, false);
            MarkPlaced();

            await _instance.InvokeVoidAsync("setFilter", new { dbTables = _dbTables?.Select(t => t.Name).ToArray() });
            await PushFilterAsync();

            _graphDirty = false;
            _itemsDirty = false;
            _loaded = new LoadedDb(rid, item?.Name ?? rid.ToString(),
                item?.Others.GetValueOrDefault("db_schema"), item?.Others.GetValueOrDefault("db_type"));

            return _summary.Nodes;
        }, "도면이 비어 있습니다. 「표 놓기」로 표를 놓으십시오.", "계통도를 읽지 못했습니다");
    }

    /// <summary>같은 이름이 여럿이면 마지막에 고친 것. 이 표에는 제약이 없어 겹친 줄이 생길 수 있다.</summary>
    private static ProjectDbPropDto? Latest(IReadOnlyList<ProjectDbPropDto> props, string key) =>
        props.Where(p => string.Equals(p.DbPkey, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.ModDt ?? p.CreDt)
            .ThenByDescending(p => p.DbPrid)
            .FirstOrDefault();

    /// <summary>대상 DB 의 표 목록. 못 읽어도 던지지 않는다 — 폐쇄망 DB 처럼 붙을 수 없는 접속에서도 저장된 도면은 열려야 한다.</summary>
    private async Task<(List<LineageDbTable>?, string?)> ReadDbTablesAsync(CommonCodeItem? item)
    {
        var args = new Dictionary<string, object?>
        {
            ["db"] = item?.Others.GetValueOrDefault("db_type") ?? string.Empty,
            ["dbnick"] = item?.Others.GetValueOrDefault("db_nick") ?? string.Empty,
            ["db_rid"] = _dbCode ?? string.Empty,
        };

        try
        {
            var r = await Client.JsContAsync("tablelist", args);
            if (r.ProcCode < 0)
            {
                return (null, $"대상 DB 에 붙지 못해 저장된 도면만 그렸습니다 — {r.Message?.Trim()}");
            }

            var list = (r.Rows ?? [])
                .Select(row => new LineageDbTable
                {
                    Name = Pick(row, "TableName", "tablename"),
                    Description = Pick(row, "Description", "description"),
                })
                .Where(t => t.Name.Length > 0)
                .ToList();

            return (list, null);
        }
        catch (ApiException ex)
        {
            return (null, $"대상 DB 에 붙지 못해 저장된 도면만 그렸습니다 — {ex.Message}");
        }
    }

    private static string Pick(ProjMngRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    // ───────────────────────── 저장 ─────────────────────────

    private async Task SaveAsync()
    {
        if (_loaded is null || _instance is null) return;
        if (!CanEdit) return;

        if (!Dirty)
        {
            Say("바뀐 것이 없습니다.");
            return;
        }

        var loaded = _loaded;
        var graph = _graphDirty ? await _instance.InvokeAsync<string>("save") : null;
        var items = _itemsDirty ? _items.ToJson() : null;

        var ok = await RunAsync(async () =>
        {
            var props = await Dbs.PropsAsync(loaded.DbRid);

            if (graph is not null)
            {
                _graphProp = await WriteAsync(loaded.DbRid, props, _graphProp, LineageGraph.GraphKey, graph, "테이블 계통도");
                _graphDirty = false;
            }

            if (items is not null)
            {
                _itemsProp = await WriteAsync(loaded.DbRid, props, _itemsProp, LineageGraph.ItemsKey, items, "테이블 계통도 업무항목");
                _itemsDirty = false;
            }
        }, "저장했습니다.", "저장하지 못했습니다");

        if (!ok && graph is not null)
        {
            // 캔버스는 저장을 넘긴 순간 「저장됨」으로 알고 있다. 다음 손질에서 다시 알리도록 둔다.
            _graphDirty = true;
        }
    }

    private async Task<ProjectDbPropDto?> WriteAsync(
        int dbRid, IReadOnlyList<ProjectDbPropDto> props, ProjectDbPropDto? known,
        string key, string value, string comment)
    {
        var current = known is null
            ? Latest(props, key)
            : props.FirstOrDefault(p => p.DbPrid == known.DbPrid);

        // 읽은 뒤에 누군가 저장했다 — 덮지 않는다. 나중 사람이 앞사람 것을 조용히 지우는 일을 막는다.
        if (current is not null && (known is null || current.ModDt != known.ModDt))
        {
            throw new ApiException($"그 사이 다른 사람이 {comment}를 저장했습니다. 「불러오기」로 지금 것을 받은 뒤 다시 고치십시오.");
        }

        if (current is null)
        {
            return await Dbs.CreatePropAsync(dbRid, new ProjectDbPropDto
            {
                DbRid = dbRid,
                DbPkey = key,
                DbPvalue = value,
                DbPtype = LineageGraph.PropType,
                DbPcomment = comment,
            });
        }

        current.DbPvalue = value;
        current.DbPtype ??= LineageGraph.PropType;
        current.DbPcomment ??= comment;

        return await Dbs.UpdatePropAsync(dbRid, current)
               ?? throw new ApiException($"{comment} 줄을 찾지 못했습니다.");
    }

    private async Task ResetToSeedAsync()
    {
        if (_seed is null || _instance is null) return;

        if (!_resetArmed)
        {
            _resetArmed = true;
            return;
        }

        _resetArmed = false;
        _summary = await _instance.InvokeAsync<LineageSummary>("load", _seed.DbPvalue ?? LineageGraph.EmptyGraph, true);
        MarkPlaced();
        await PushFilterAsync();
        _selNode = null;
        _selEdge = null;
        _graphDirty = true;
        Say("처음 배치로 되돌렸습니다. 「저장」을 눌러야 남습니다. 업무항목은 그대로입니다.", NoticeTone.Warning);
    }

    // ───────────────────────── 캔버스가 올려 주는 것 ─────────────────────────

    [JSInvokable]
    public Task NotifyChanged() => InvokeAsync(() =>
    {
        _graphDirty = true;
        StateHasChanged();
    });

    [JSInvokable]
    public Task NotifySelect(string? kind, string? json) => InvokeAsync(() =>
    {
        _selNode = null;
        _selEdge = null;

        if (kind == "node" && json is not null)
        {
            _selNode = JsonSerializer.Deserialize<LineageNode>(json, LineageGraph.Json);
            if (_selNode is not null)
            {
                _edit = new LineageNode
                {
                    Id = _selNode.Id, Kr = _selNode.Kr, Kind = _selNode.Kind ?? "master", Pk = _selNode.Pk,
                    Owner = _selNode.Owner, Note = _selNode.Note, Warn = _selNode.Warn,
                };
                _editId = _selNode.Id;
                _editStar = (_selNode.Star ?? 0) > 0;
            }
        }
        else if (kind == "edge" && json is not null)
        {
            _selEdge = JsonSerializer.Deserialize<LineageEdge>(json, LineageGraph.Json);
            if (_selEdge is not null)
            {
                _edgeEdit = new LineageEdge
                {
                    Id = _selEdge.Id, Label = _selEdge.Label, Ty = _selEdge.Ty, Key = _selEdge.Key,
                    Seq = _selEdge.Seq, Flow = _selEdge.Flow,
                    SourceHandle = _selEdge.SourceHandle, TargetHandle = _selEdge.TargetHandle,
                };
            }
        }

        StateHasChanged();
    });

    [JSInvokable]
    public Task NotifyLane(int lane) => InvokeAsync(() => SetLaneAsync(_lane == lane ? null : lane));

    /// <summary>편집모드에서 칸을 눌렀다 — 고른 업무항목에 넣거나 뺀다.</summary>
    [JSInvokable]
    public Task NotifyPick(string id) => InvokeAsync(async () =>
    {
        if (CurItem is not { } item || !CanEdit) return;

        if (!item.Tables.Remove(id))
        {
            item.Tables.Add(id);
        }

        _itemsDirty = true;
        await PushFilterAsync();
        StateHasChanged();
    });

    [JSInvokable]
    public Task NotifyDeleteKey() => InvokeAsync(async () =>
    {
        if (CanEdit) await RemoveSelectionAsync();
    });

    [JSInvokable]
    public Task NotifySave() => InvokeAsync(SaveAsync);

    [JSInvokable]
    public Task NotifyNotice(string text) => InvokeAsync(() => Say(text, NoticeTone.Info));

    [JSInvokable]
    public Task NotifyLinking(bool on) => InvokeAsync(() =>
    {
        _linking = on;
        StateHasChanged();
    });

    // ───────────────────────── 거르개 ─────────────────────────

    private async Task SetQueryAsync(string text)
    {
        _q = text ?? string.Empty;
        _foundAt = 0;
        await PushFilterAsync();
    }

    private async Task OnFindKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await GoFoundAsync();
    }

    /// <summary>걸린 칸을 차례로 찾아간다. 누를 때마다 다음 것이다.</summary>
    private async Task GoFoundAsync()
    {
        var found = Found;
        if (found.Count == 0) return;
        var t = found[_foundAt % found.Count];
        _foundAt++;
        await GoToAsync(t.Id);
    }

    private async Task ToggleKindAsync(string ty)
    {
        if (!_kinds.Remove(ty)) _kinds.Add(ty);
        await PushFilterAsync();
    }

    private async Task SetAllKindsAsync(bool on)
    {
        _kinds.Clear();
        if (on)
        {
            foreach (var k in LineageGraph.EdgeKinds) _kinds.Add(k.Value);
        }
        await PushFilterAsync();
    }

    private async Task SetOnlyLinkedAsync(bool on)
    {
        _onlyLinked = on;
        await PushFilterAsync();
    }

    private async Task SetHoverAsync(bool on)
    {
        _hover = on;
        await PushFilterAsync();
    }

    private async Task ToggleFlowAsync(string id)
    {
        if (!_flows.Remove(id)) _flows.Add(id);
        await PushFilterAsync();
    }

    private async Task SetLaneAsync(int? lane)
    {
        _lane = lane;
        await PushFilterAsync();
        StateHasChanged();
    }

    private async Task ClearFiltersAsync()
    {
        _q = string.Empty;
        _flows.Clear();
        _lane = null;
        _itemId = null;
        _onlyLinked = false;
        _solo = false;
        _picking = false;
        await PushFilterAsync();
    }

    // ───────────────────────── 업무항목 ─────────────────────────

    private async Task PickItemAsync(string id)
    {
        _itemId = _itemId == id ? null : id;
        if (_itemId is null) _picking = false;
        await PushFilterAsync();
    }

    private async Task SetSoloAsync(bool on)
    {
        _solo = on;
        await PushFilterAsync();
    }

    private async Task SetPickingAsync(bool on)
    {
        _picking = on;
        await PushFilterAsync();
    }

    private async Task AddItemAsync()
    {
        var name = _newItem?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var item = new LineageItem
        {
            Id = "itm" + Guid.NewGuid().ToString("N")[..10],
            Name = name.Length > 120 ? name[..120] : name,
        };
        _items.Items.Add(item);
        _newItem = null;
        _itemsDirty = true;

        // 만들자마자 담는 일을 하게 된다 — 고르고 편집모드를 켜 둔다.
        _itemId = item.Id;
        _picking = true;
        await PushFilterAsync();
        Say("칸을 눌러 이 업무항목에 담으십시오. 다시 누르면 빠집니다.");
    }

    private void RenameItem(LineageItem item)
    {
        var name = _renameText?.Trim();
        if (!string.IsNullOrEmpty(name) && name != item.Name)
        {
            item.Name = name.Length > 120 ? name[..120] : name;
            _itemsDirty = true;
        }
        _renaming = null;
    }

    private async Task DropItemAsync(LineageItem item)
    {
        _items.Items.Remove(item);
        _itemsDirty = true;
        if (_itemId == item.Id)
        {
            _itemId = null;
            _picking = false;
            await PushFilterAsync();
        }
    }

    // ───────────────────────── 고치기 ─────────────────────────

    private async Task ToggleLinkAsync()
    {
        if (_instance is null) return;
        _linking = await _instance.InvokeAsync<bool>("setLinking", !_linking, NewKind.Value);
    }

    private async Task SetNewKindAsync(LineageKind? kind)
    {
        NewKind = kind ?? LineageGraph.EdgeKinds[0];
        if (_instance is not null && _linking) await _instance.InvokeAsync<bool>("setLinking", true, NewKind.Value);
    }

    private async Task RemoveSelectionAsync()
    {
        if (_instance is null) return;

        var id = _selNode?.Id;
        var removed = await _instance.InvokeAsync<int>("removeSelection");
        if (removed == 0) return;

        // 지운 칸이 업무항목에 담겨 있으면 거기서도 뺀다 — 남기면 없는 표를 가리킨다.
        if (id is not null)
        {
            foreach (var it in _items.Items)
            {
                if (it.Tables.Remove(id)) _itemsDirty = true;
            }
        }

        await RefreshSummaryAsync();
    }

    private async Task ApplyNodeAsync()
    {
        if (_instance is null || _selNode is null) return;

        var oldId = _selNode.Id;
        var newId = _editId?.Trim() ?? oldId;
        var patch = new
        {
            id = newId,
            kr = _edit.Kr ?? string.Empty,
            kind = _edit.Kind ?? "master",
            pk = _edit.Pk ?? string.Empty,
            owner = _edit.Owner ?? string.Empty,
            note = _edit.Note ?? string.Empty,
            warn = _edit.Warn ?? string.Empty,
            star = _editStar ? 1 : 0,
        };

        var result = await _instance.InvokeAsync<string>("patchNode", oldId, JsonSerializer.Serialize(patch));
        if (result == "dup")
        {
            Say($"{newId} 은(는) 이미 도면에 있습니다.", NoticeTone.Warning);
            return;
        }

        // 이름이 바뀌었으면 업무항목이 든 이름도 함께 옮긴다.
        if (newId.Length > 0 && newId != oldId)
        {
            foreach (var it in _items.Items)
            {
                var i = it.Tables.IndexOf(oldId);
                if (i >= 0)
                {
                    it.Tables[i] = newId;
                    _itemsDirty = true;
                }
            }
        }

        await RefreshSummaryAsync();
    }

    private Task ApplyEdgeAsync() => PatchEdgeAsync(new
    {
        label = _edgeEdit.Label ?? string.Empty,
        ty = _edgeEdit.Ty,
        key = _edgeEdit.Key,
        seq = _edgeEdit.Seq,
        flow = _edgeEdit.Flow,
        sourceHandle = _edgeEdit.SourceHandle,
        targetHandle = _edgeEdit.TargetHandle,
    });

    private async Task PatchEdgeAsync(object patch)
    {
        if (_instance is null || _selEdge is null) return;
        await _instance.InvokeAsync<bool>("patchEdge", _selEdge.Id, JsonSerializer.Serialize(patch));
        await RefreshSummaryAsync();
    }

    private async Task GoToAsync(string id)
    {
        if (_instance is null) return;
        if (!await _instance.InvokeAsync<bool>("goTo", id))
        {
            Say($"{id} 은(는) 이 도면에 없습니다.", NoticeTone.Warning);
        }
    }

    private async Task SelectEdgeAsync(string edgeId)
    {
        if (_instance is not null) await _instance.InvokeVoidAsync("select", new { kind = "edge", id = edgeId });
    }

    // ───────────────────────── 표 놓기 ─────────────────────────

    private Task OpenPickerAsync()
    {
        _picked = [];
        _manualId = null;
        _manualKr = null;
        _pickerOpen = true;
        return Task.CompletedTask;
    }

    private async Task PlaceAsync()
    {
        if (_instance is null) return;

        var list = _picked.Where(t => !t.Placed)
            .Select(t => new { id = t.Name, kr = t.Description ?? string.Empty, kind = _pickKind, owner = string.Empty })
            .ToList();

        if (!string.IsNullOrWhiteSpace(_manualId))
        {
            list.Add(new { id = _manualId.Trim(), kr = _manualKr?.Trim() ?? string.Empty, kind = _pickKind, owner = string.Empty });
        }

        var placed = await _instance.InvokeAsync<int>("addTables", JsonSerializer.Serialize(list));
        await RefreshSummaryAsync();
        _picked = [];
        _manualId = null;
        _manualKr = null;
        _pickerOpen = false;

        Say(placed == list.Count
            ? $"표 {placed}개를 놓았습니다. 깜빡이는 칸을 끌어 자리를 잡으십시오."
            : $"표 {placed}개를 놓았습니다 — {list.Count - placed}개는 이미 도면에 있어 건너뛰었습니다.");
    }

    /// <summary>설명이 빈 칸만 대상 DB 의 코멘트로 채운다. 사람이 적은 설명은 덮지 않는다.</summary>
    private async Task FillCommentsAsync()
    {
        if (_instance is null || _dbTables is null) return;

        var map = _dbTables.Where(t => !string.IsNullOrWhiteSpace(t.Description))
            .GroupBy(t => t.Name.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Description);

        var n = await _instance.InvokeAsync<int>("fillComments", JsonSerializer.Serialize(map));
        await RefreshSummaryAsync();
        Say($"설명 {n}개를 채웠습니다.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_instance is not null)
            {
                await _instance.InvokeVoidAsync("destroy");
                await _instance.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // 회로가 먼저 끊겼다. 정리할 브라우저가 이미 없다.
        }
        catch (ObjectDisposedException)
        {
        }

        _self?.Dispose();
    }
}
