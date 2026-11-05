using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class OrgChart
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(_companies, c => c.Id, c => c.Name, _companyId, "회사를 고르세요"),
        _vertical ? "세로" : "가로");

    /// <summary>이동 팝업에서 「회사 직속」을 뜻하는 값. 부서를 옮길 때만 나온다.</summary>
    private const string ToCompany = "__COMPANY__";

    /// <summary>가상 부서 「미소속부서」의 이름과 식별자 앞머리. 식별자는 실제 부서·사람과 겹치지 않게 회사 식별자를 붙인다.</summary>
    private const string NoDeptName = "미소속부서";

    private const string NoDeptIdPrefix = "__NODEPT__:";

    private enum OrgKind { Company, Dept, User }

    /// <summary>그려질 노드 하나. 회사·부서·사람이 같은 모양으로 들어온다 — 배치와 연결선이 셋을 가리지 않기 때문이다.</summary>
    private sealed class Node
    {
        public string Id { get; init; } = string.Empty;
        public OrgKind Kind { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Depth { get; init; }
        public string? ParentId { get; init; }
        public List<Node> Children { get; } = [];

        public string? LoginId { get; init; }
        public string? Email { get; init; }
        public string? Avatar { get; init; }

        /// <summary>이 부서에 직접 속한 사람 수. 부서 노드만 쓴다.</summary>
        public int UserCount { get; init; }

        /// <summary>「미소속부서」— 부서 없이 회사에만 속한 사람을 담는 가상 부서. DB 에 없으므로 끌기·이동·하위 추가를 막고, 여기에 놓으면 회사 직속이 된다.</summary>
        public bool IsNoDept { get; init; }

        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>선을 받아 올 자리. 배치가 끝난 뒤 채운다.</summary>
        public double? ParentX { get; set; }

        public double? ParentY { get; set; }

        /// <summary>세로 조직도에서 부서 아래 쌓인 사람의 바로 위 노드. 부챗살로 그으면 한 칸에 겹치므로 위에서 아래로 이어 긋는 값이다.</summary>
        public string? StackPrevId { get; set; }
    }

    private sealed record Choice(Node Node, int Depth);

    // 카드 180×50. 원본(Vue)의 값을 그대로 옮겼다 — 눈대중으로 다시 잡으면 선이 카드에 닿거나 멀어진다.

    private const double CardWidth = 180;
    private const double CardHeight = 50;

    /// <summary>부서 아래 사람을 쌓는 간격. 카드 높이보다 8px 넓다.</summary>
    private const double UserStackGap = 58;

    private IReadOnlyList<CompanyDto> _companies = [];
    private string? _companyId;
    private string _companyName = "회사";

    private Node? _root;
    private List<Node> _nodes = [];
    private double _stageWidth = 1;
    private double _stageHeight = 1;

    private bool _vertical = true;

    private bool _showUnassigned;
    private IReadOnlyList<AccountDto> _unassigned = [];

    // 부서 추가
    private bool _adding;
    private Node? _addParent;
    private string _addName = string.Empty;

    // 이동
    private bool _moving;
    private OrgKind _moveKind;
    private string? _moveId;
    private string _moveName = string.Empty;
    private string? _moveTarget;

    /// <summary>끌기 값(<c>data-drag</c>)의 앞머리. org-chart.js 가 첫 쌍점에서 갈라 <see cref="DropFromJs"/> 로 되돌려 준다.</summary>
    private const string UserDragPrefix = "user:";

    private const string DeptDragPrefix = "dept:";

    private ElementReference _wrap;
    private ElementReference _viewport;
    private ElementReference _stage;
    private IJSObjectReference? _module;
    private IJSObjectReference? _canvas;
    private DotNetObjectReference<OrgChart>? _self;

    /// <summary>다시 그린 뒤 화면을 맞춰야 하는가. 조회·방향 전환 뒤에 선다.</summary>
    private bool _needsFit;

    private string AddTitle => _addParent is null
        ? $"[{_companyName}] 회사 직속 부서 추가"
        : $"[{_addParent.Name}] 하위 부서 추가";

    private string MoveTitle =>
        $"[{_moveName}] {(_moveKind == OrgKind.Dept ? "부서" : "사용자")} 이동";

    /// <summary>옮길 수 있는가. 단추는 PermissionView 가 감추지만 끌기는 속성이라 여기서 따로 본다 — 실제 통제는 서버가 한다.</summary>
    private bool CanMove => Can(MenuAction.Update);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(async () =>
        {
            _companies = await Api.GetCompaniesAsync();

            // 회사를 고르지 않으면 아무것도 안 나오는 화면이라 첫 회사를 잡아 준다.
            _companyId ??= _companies.FirstOrDefault()?.Id;
            return _companies.Count;
        }, "등록된 회사가 없습니다.", "회사 목록을 읽지 못했습니다");

        if (!string.IsNullOrEmpty(_companyId))
        {
            await ReloadAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_module is null)
        {
            _module = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/org-chart.js");
            _self = DotNetObjectReference.Create(this);
            _canvas = await _module.InvokeAsync<IJSObjectReference>("create", _viewport, _stage, _wrap, _self);
        }

        if (_needsFit && _canvas is not null)
        {
            _needsFit = false;
            await _canvas.InvokeVoidAsync("fit", _stageWidth, _stageHeight);
        }
    }

    private async Task PickCompanyAsync(string? companyId)
    {
        _companyId = companyId;
        await ReloadAsync();
    }

    /// <summary>세로·가로를 바꾼다. 자료는 그대로고 배치만 다시 잡는다.</summary>
    private void SetDirection(bool vertical)
    {
        if (_vertical == vertical) return;

        _vertical = vertical;
        Layout();
        _needsFit = true;
    }

    /// <summary>조직도를 다시 읽는다. 부서를 먼저 받고 사람은 몇 개씩 묶어 나란히 받는다 — 한꺼번에 던지면 게이트웨이로 수십 개가 동시에 나가고, 하나씩 기다리면 부서 수만큼 왕복한다.</summary>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrEmpty(_companyId))
        {
            _root = null;
            _nodes = [];
            return 0;
        }

        var companyId = _companyId;

        // 부서 없이 회사에만 속한 사람도 정상 소속이다 — 회사 아래 가상 부서 「미소속부서」에 담아 그린다.
        // 이 조회는 무소속(회사도 없는) 사람까지 함께 주므로 회사로 한 번 더 거른다.
        var deptsTask = Api.GetDeptsAsync(companyId);
        var directTask = Api.GetDeptEligibleUsersAsync(companyId);

        await Task.WhenAll(deptsTask, directTask);

        var depts = deptsTask.Result;
        var direct = directTask.Result
            .Where(u => string.Equals(u.CompanyId, companyId, StringComparison.Ordinal))
            .ToList();

        var flat = new List<DeptDto>();
        Flatten(depts, flat);

        var people = new Dictionary<string, IReadOnlyList<AccountDto>>(StringComparer.Ordinal);

        foreach (var chunk in flat.Chunk(6))
        {
            var loaded = await Task.WhenAll(chunk.Select(async d => (d.Id, Users: await Api.GetDeptUsersAsync(d.Id))));

            foreach (var (id, users) in loaded)
            {
                people[id] = users;
            }
        }

        _companyName = flat.FirstOrDefault()?.CompanyName
            ?? _companies.FirstOrDefault(c => c.Id == _companyId)?.Name
            ?? "회사";

        _root = new Node
        {
            Id = companyId,
            Kind = OrgKind.Company,
            Name = _companyName,
            Depth = 0,
        };

        _root.Children.AddRange(BuildDepts(depts, people, 1));

        if (direct.Count > 0)
        {
            var noDept = new Node
            {
                Id = NoDeptIdPrefix + companyId,
                Kind = OrgKind.Dept,
                Name = NoDeptName,
                Depth = 1,
                UserCount = direct.Count,
                IsNoDept = true,
            };

            noDept.Children.AddRange(direct.Select(u => UserNode(u, noDept.Id, 2)));
            _root.Children.Add(noDept);
        }

        Layout();
        _needsFit = true;

        return flat.Count + direct.Count;
    }, "이 회사에는 등록된 부서도 소속 인원도 없습니다.", "조직도를 읽지 못했습니다");

    private static List<Node> BuildDepts(
        IEnumerable<DeptDto> depts,
        IReadOnlyDictionary<string, IReadOnlyList<AccountDto>> people,
        int depth)
    {
        var nodes = new List<Node>();

        foreach (var dept in depts)
        {
            var node = new Node
            {
                Id = dept.Id,
                Kind = OrgKind.Dept,
                Name = dept.Name,
                Depth = depth,
                ParentId = dept.Pid,
                UserCount = dept.UserCount,
            };

            // 사람이 먼저, 하위 부서가 나중이다. 세로 조직도에서 사람은 한 칸에
            // 쌓이고 부서는 옆으로 갈라지므로 순서가 곧 그림의 순서가 된다.
            if (people.TryGetValue(dept.Id, out var users))
            {
                node.Children.AddRange(users.Select(u => UserNode(u, dept.Id, depth + 1)));
            }

            node.Children.AddRange(BuildDepts(dept.Children ?? [], people, depth + 1));
            nodes.Add(node);
        }

        return nodes;
    }

    private static Node UserNode(AccountDto u, string parentId, int depth) => new()
    {
        Id = u.Id,
        Kind = OrgKind.User,
        Name = string.IsNullOrWhiteSpace(u.UserName) ? u.LoginId : u.UserName,
        Depth = depth,
        ParentId = parentId,
        LoginId = u.LoginId,
        Email = u.Email,
        Avatar = u.Avatar,
    };

    // 원본(Vue)의 계산을 그대로 옮겼다. 사람을 한 칸에 쌓는 것이 핵심이다 — 사람마다 한 칸을 쓰면 여덟 명짜리 부서 하나가 1600px 을 먹는다.

    private void Layout()
    {
        _nodes = [];

        var root = _root;

        if (root is null)
        {
            _stageWidth = 1;
            _stageHeight = 1;
            return;
        }

        var xGap = _vertical ? 200d : 250d;
        var yGap = _vertical ? 120d : 70d;

        var xCounter = 0;
        var yCounter = 0;

        void Traverse(Node node)
        {
            node.X = _vertical ? xCounter * xGap : node.Depth * xGap;
            node.Y = _vertical ? node.Depth * yGap : yCounter * yGap;
            node.StackPrevId = null;
            _nodes.Add(node);

            var stacked = _vertical
                ? node.Children.Where(c => c.Kind == OrgKind.User).ToList()
                : new List<Node>();

            var branching = _vertical
                ? node.Children.Where(c => c.Kind != OrgKind.User).ToList()
                : node.Children;

            if (stacked.Count > 0)
            {
                // 사람이 몇이든 한 칸이다. 부서의 x 는 자식들의 가운데로 보정되므로
                // 사람 묶음에도 자기 칸이 있어야 하위 부서 칸과 겹치지 않는다.
                var column = xCounter;
                xCounter++;

                for (var i = 0; i < stacked.Count; i++)
                {
                    var user = stacked[i];
                    user.X = column * xGap;
                    user.Y = ((node.Depth + 1) * yGap) + (i * UserStackGap);
                    user.StackPrevId = i == 0 ? node.Id : stacked[i - 1].Id;
                    _nodes.Add(user);
                }
            }

            if (branching.Count > 0)
            {
                foreach (var child in branching)
                {
                    Traverse(child);
                }
            }
            else if (stacked.Count == 0)
            {
                // 자식이 없는 잎이다. 자기 칸을 하나 쓴다.
                if (_vertical)
                {
                    xCounter++;
                }
                else
                {
                    yCounter++;
                }
            }
        }

        Traverse(root);
        Center(root);

        // 같은 식별자가 두 번 오는 일은 없어야 하지만, 그것 때문에 화면이 통째로
        // 죽지는 않게 한다(ToDictionary 는 던진다).
        var byId = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var node in _nodes)
        {
            byId[node.Id] = node;
        }

        foreach (var node in _nodes)
        {
            node.ParentX = null;
            node.ParentY = null;

            // 쌓인 사람은 부서가 아니라 바로 위 노드에서 선을 받는다.
            // 최상위 부서(Pid 가 비어 있다)는 회사에서 받는다.
            var fromId = node.StackPrevId
                ?? node.ParentId
                ?? (ReferenceEquals(node, root) ? null : root.Id);

            if (fromId is not null && byId.TryGetValue(fromId, out var from) && !ReferenceEquals(from, node))
            {
                node.ParentX = from.X;
                node.ParentY = from.Y;
            }
        }

        _stageWidth = _nodes.Count == 0 ? 1 : _nodes.Max(n => n.X) + CardWidth + 10;
        _stageHeight = _nodes.Count == 0 ? 1 : _nodes.Max(n => n.Y) + CardHeight + 20;
    }

    /// <summary>부모를 자식들 가운데로 옮긴다. 아래에서 위로 올라오며 맞춘다.</summary>
    private void Center(Node node)
    {
        if (node.Children.Count == 0) return;

        foreach (var child in node.Children)
        {
            Center(child);
        }

        if (_vertical)
        {
            node.X = (node.Children.Min(c => c.X) + node.Children.Max(c => c.X)) / 2;
        }
        else
        {
            node.Y = (node.Children.Min(c => c.Y) + node.Children.Max(c => c.Y)) / 2;
        }
    }

    /// <summary>연결선. 3차 베지어로 긋고 조절점은 두 끝 사이에서 잡는다 — 카드 좌표 사이에서 잡으면 가로 조직도에서 선이 뒤로 물러났다 가는 S 자가 된다.</summary>
    private string LinkPath(Node node)
    {
        var sx = node.ParentX!.Value;
        var sy = node.ParentY!.Value;

        if (_vertical)
        {
            // 쌓인 사람 사이는 8px 이다. 그 사이에 곡선을 그으면 조절점이
            // 시작점보다 **위**로 잡혀 선이 되돌아 휜다 — 짧은 직선이 맞다.
            if (node.StackPrevId is not null)
            {
                return $"M {N(sx + (CardWidth / 2))} {N(sy + CardHeight)} " +
                       $"L {N(node.X + (CardWidth / 2))} {N(node.Y)}";
            }

            var cy = ((sy + CardHeight) + node.Y) / 2;

            return $"M {N(sx + (CardWidth / 2))} {N(sy + CardHeight)} " +
                   $"C {N(sx + (CardWidth / 2))} {N(cy)}, " +
                   $"{N(node.X + (CardWidth / 2))} {N(cy)}, " +
                   $"{N(node.X + (CardWidth / 2))} {N(node.Y)}";
        }

        var cx = ((sx + CardWidth) + node.X) / 2;

        return $"M {N(sx + CardWidth)} {N(sy + (CardHeight / 2))} " +
               $"C {N(cx)} {N(sy + (CardHeight / 2))}, " +
               $"{N(cx)} {N(node.Y + (CardHeight / 2))}, " +
               $"{N(node.X)} {N(node.Y + (CardHeight / 2))}";
    }

    // ── 끌어다 놓기 ──────────────────────────────────────────

    /// <summary>카드를 다른 카드에 놓았다(org-chart.js). 놓는 자리는 <see cref="DropKey"/> 가 정해 둔 노드 식별자다.</summary>
    [JSInvokable]
    public async Task DropFromJs(string kind, string id, string targetId)
    {
        var target = _nodes.FirstOrDefault(n => string.Equals(n.Id, targetId, StringComparison.Ordinal));
        if (target is null) return;

        var dragKind = kind == "dept" ? OrgKind.Dept : OrgKind.User;

        await DropAsync(dragKind, id, target);
        StateHasChanged();
    }

    /// <summary>노드 위에 놓았다. 회사나 「미소속부서」에 놓으면 회사 직속(사람은 부서 없음 → 미소속부서)으로, 부서에 놓으면 그 아래로 간다.</summary>
    private async Task DropAsync(OrgKind kind, string id, Node target)
    {
        if (!CanMove || target.Kind == OrgKind.User) return;
        if (string.Equals(id, target.Id, StringComparison.Ordinal)) return;

        var targetDeptId = target.Kind == OrgKind.Company || target.IsNoDept ? null : target.Id;

        if (kind == OrgKind.User)
        {
            // 이미 그 자리에 있으면 저장하지 않는다 — 살짝 끌었다 놓은 것으로 「옮겼습니다」가 뜨면 안 된다.
            var current = _nodes.FirstOrDefault(n => n.Kind == OrgKind.User && n.Id == id);
            var currentDeptId = current?.ParentId?.StartsWith(NoDeptIdPrefix, StringComparison.Ordinal) == true ? null : current?.ParentId;

            if (current is not null && currentDeptId == targetDeptId) return;

            await ApplyMoveAsync(OrgKind.User, id, targetDeptId);
            return;
        }

        if (targetDeptId is not null && IsUnder(targetDeptId, id))
        {
            Say("부서를 자기 하위 부서 아래로 옮길 수 없습니다.", NoticeTone.Warning);
            return;
        }

        // 부서를 회사(또는 미소속부서)에 놓으면 상위가 없어진다(회사 직속).
        await ApplyMoveAsync(OrgKind.Dept, id, targetDeptId);
    }

    /// <summary><paramref name="deptId"/> 가 <paramref name="ancestorId"/> 의 하위(자기 포함)인가.</summary>
    private bool IsUnder(string deptId, string ancestorId)
    {
        var byId = _nodes.Where(n => n.Kind == OrgKind.Dept).ToDictionary(n => n.Id, StringComparer.Ordinal);

        for (var cur = deptId; cur is not null && byId.TryGetValue(cur, out var node); cur = node.ParentId)
        {
            if (cur == ancestorId) return true;
        }

        return false;
    }

    // ── 이동 팝업 (끌기의 터치 대체 경로) ────────────────────

    private void OpenMove(OrgKind kind, string id, string name)
    {
        _moveKind = kind;
        _moveId = id;
        _moveName = name;
        _moveTarget = null;
        _moving = true;
    }

    /// <summary>옮겨 갈 수 있는 부서. 받아 둔 나무를 편 것이라 서버를 다시 부르지 않는다. 자기 자신과 하위는 뺀다 — 자기 밑으로 들어가면 나무가 고리가 된다.</summary>
    private List<Choice> MoveChoices()
    {
        var choices = new List<Choice>();

        void Walk(IEnumerable<Node> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                // 「미소속부서」는 팝업 맨 위의 회사 직속 단추가 대신한다.
                if (node.Kind != OrgKind.Dept || node.IsNoDept) continue;

                if (_moveKind == OrgKind.Dept && string.Equals(node.Id, _moveId, StringComparison.Ordinal))
                {
                    // 건너뛰면 재귀도 멈춰 하위가 함께 빠진다.
                    continue;
                }

                choices.Add(new Choice(node, depth));
                Walk(node.Children, depth + 1);
            }
        }

        if (_root is not null)
        {
            Walk(_root.Children, 0);
        }

        return choices;
    }

    private async Task MoveAsync()
    {
        if (_moveId is null) return;

        if (_moveTarget is null)
        {
            Say("옮길 부서를 고르십시오.", NoticeTone.Warning);
            return;
        }

        var target = _moveTarget == ToCompany ? null : _moveTarget;

        if (await ApplyMoveAsync(_moveKind, _moveId, target))
        {
            _moving = false;
        }
    }

    /// <summary>끌어다 놓기와 이동 팝업이 함께 쓰는 저장 경로. 둘로 나누면 한쪽에만 규칙이 붙는다. 부서를 비우면 지금 보는 회사의 직속이다.</summary>
    private async Task<bool> ApplyMoveAsync(OrgKind kind, string id, string? targetDeptId)
    {
        if (targetDeptId is null && string.IsNullOrEmpty(_companyId))
        {
            Say("회사를 먼저 고르십시오.", NoticeTone.Warning);
            return false;
        }

        var ok = await RunAsync(
            () => kind == OrgKind.Dept
                ? Api.MoveDeptAsync(id, targetDeptId)
                : Api.MoveUserDeptAsync(id, targetDeptId, targetDeptId is null ? _companyId : null),
            targetDeptId is null
                ? kind == OrgKind.Dept ? "부서를 회사 직속으로 옮겼습니다." : "사람을 미소속부서(부서 없이 회사 소속)로 옮겼습니다."
                : kind == OrgKind.Dept ? "부서를 옮겼습니다." : "소속을 바꿨습니다.",
            "옮기지 못했습니다");

        if (!ok) return false;

        await ReloadAsync();

        // 미소속에서 끌어온 사람은 이제 소속(회사)이 있다. 목록을 다시 받는다.
        if (_showUnassigned)
        {
            await LoadUnassignedAsync();
        }

        return true;
    }

    // ── 부서 추가 ────────────────────────────────────────────

    private void OpenAddDept(Node? parent)
    {
        _addParent = parent;
        _addName = string.Empty;
        _adding = true;
    }

    private async Task AddDeptAsync()
    {
        if (string.IsNullOrWhiteSpace(_addName))
        {
            Say("부서명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_companyId))
        {
            Say("회사를 먼저 고르십시오.", NoticeTone.Warning);
            return;
        }

        var saved = await RunAsync(
            () => Api.CreateDeptAsync(new SaveDeptDto
            {
                Name = _addName.Trim(),
                Pid = _addParent?.Id,
                CompanyId = _companyId,
                Status = 1,
            }),
            _addParent is null ? "회사 직속 부서를 만들었습니다." : $"[{_addParent.Name}] 아래에 부서를 만들었습니다.",
            "부서를 만들지 못했습니다");

        if (saved)
        {
            _adding = false;
            await ReloadAsync();
        }
    }

    // ── 미소속 사용자 ────────────────────────────────────────

    private async Task ToggleUnassignedAsync()
    {
        _showUnassigned = !_showUnassigned;

        if (_showUnassigned)
        {
            await LoadUnassignedAsync();
        }
    }

    /// <summary>회사 인자를 넘기지 않는다 — 어느 회사에도 없는 사람이라 후보가 어느 회사 조직도에서나 같다.</summary>
    private Task LoadUnassignedAsync() => LoadAsync(async () =>
    {
        _unassigned = await Api.GetDeptEligibleUsersAsync();
        return 1;   // 비어 있어도 옆 목록이 스스로 말한다. 안내 줄까지 쓰지 않는다.
    }, failMessage: "미소속 사용자를 읽지 못했습니다");

    private Task FitAsync() =>
        _canvas is null
            ? Task.CompletedTask
            : _canvas.InvokeVoidAsync("fit", _stageWidth, _stageHeight).AsTask();

    private Task ZoomAsync(string method) =>
        _canvas is null ? Task.CompletedTask : _canvas.InvokeVoidAsync(method).AsTask();

    // ── 잔손질 ──────────────────────────────────────────────

    private static void Flatten(IEnumerable<DeptDto> nodes, List<DeptDto> into)
    {
        foreach (var node in nodes)
        {
            into.Add(node);

            if (node.Children is { Count: > 0 })
            {
                Flatten(node.Children, into);
            }
        }
    }

    private static string Kind(Node node) => node.Kind switch
    {
        OrgKind.Company => "company",
        OrgKind.Dept => node.IsNoDept ? "dept ad-orgc__node--nodept" : "dept",
        _ => "user",
    };

    private string Sub(Node node) => node.Kind switch
    {
        OrgKind.Company => "회사",
        OrgKind.Dept => $"{node.UserCount}명",
        _ => node.Email ?? node.LoginId ?? string.Empty,
    };

    private string? DragKey(Node node) =>
        !CanMove || node.Kind == OrgKind.Company || node.IsNoDept ? null
        : (node.Kind == OrgKind.User ? UserDragPrefix : DeptDragPrefix) + node.Id;

    /// <summary>놓는 자리. 사람 카드에 놓으면 그 사람이 있는 부서(미소속부서 포함)에 놓은 것으로 친다.</summary>
    private string? DropKey(Node node) =>
        !CanMove ? null : node.Kind == OrgKind.User ? node.ParentId : node.Id;

    /// <summary>인라인 스타일에 쓸 픽셀 값. 자리 표기가 지역에 따라 흔들리면 안 된다.</summary>
    private static string Px(double value) => N(value) + "px";

    private static string N(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public async ValueTask DisposeAsync()
    {
        // 회로가 이미 끊겼으면 JS 를 부를 수 없다. 화면을 옮기며 늘 지나는 길이라
        // 예외를 남기지 않는다.
        try
        {
            if (_canvas is not null)
            {
                await _canvas.InvokeVoidAsync("dispose");
                await _canvas.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }

        _self?.Dispose();
    }
}
