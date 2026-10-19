using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class ParticipationBoard
{
    [Inject] private ProjectUserClient Api { get; set; } = default!;
    [Inject] private BizOptions Accounts { get; set; } = default!;

    /// <summary>어느 쪽을 기준으로 볼 것인가.</summary>
    public enum BoardAxis
    {
        /// <summary>왼쪽이 프로젝트, 오른쪽이 사람.</summary>
        Project,

        /// <summary>왼쪽이 사람, 오른쪽이 프로젝트.</summary>
        Person,
    }

    /// <summary>처음 열릴 때의 기준. 메뉴 두 건이 이 값만 달리해 같은 화면을 연다. 사용자는 화면에서 뒤집을 수 있다.</summary>
    [Parameter] public BoardAxis Start { get; set; } = BoardAxis.Project;

    /// <summary>판 한 줄. 두 판이 같은 타입을 쓴다 — 축을 뒤집으면 줄이 반대편으로 가므로 타입을 갈라 두면 같은 자료를 두 번 빚게 된다.</summary>
    public sealed class BoardRow
    {
        /// <summary>프로젝트면 <c>prj_rid</c>, 사람이면 <c>user_id</c>.</summary>
        public required string Key { get; init; }

        public required string Name { get; init; }

        /// <summary>곁들이는 한 줄 — 프로젝트는 기간, 사람은 아이디.</summary>
        public required string Sub { get; init; }

        /// <summary>이 줄이 걸려 있는 짝의 수. 체크할 때 ±1 로 고친다.</summary>
        public int Count { get; set; }

        /// <summary>기준과 걸려 있는가. <b>오른쪽 판에서만 뜻이 있다.</b></summary>
        public bool On { get; set; }

        /// <summary>보내는 중. 그 줄의 체크박스만 잠근다.</summary>
        public bool Saving { get; set; }
    }

    private BoardAxis _axis;

    /// <summary>프로젝트 전부. <c>Accepted</c> 는 안 쓴다 — 짝은 <see cref="_map"/> 이 안다.</summary>
    private IReadOnlyList<ProjectAssignmentDto> _projects = [];

    /// <summary>포털 계정 전부.</summary>
    private IReadOnlyList<BizOption> _people = [];

    /// <summary>걸려 있는 짝. <c>(프로젝트 번호, 아이디)</c>.</summary>
    private readonly HashSet<(int Rid, string UserId)> _map = [];

    private List<BoardRow> _left = [];
    private List<BoardRow> _right = [];
    private BoardRow? _picked;

    private string _leftQuery = string.Empty;
    private string _rightQuery = string.Empty;

    /// <summary>아직 안 보낸 변경. <c>상대 열쇠 → 바라는 상태</c>.</summary>
    private readonly Dictionary<string, bool> _queue = new(StringComparer.Ordinal);

    /// <summary>이번 보내기 단위에서 손댄 줄들의 손대기 전 상태. 되돌리기가 이것으로 만들어진다.</summary>
    private readonly Dictionary<string, bool> _before = new(StringComparer.Ordinal);

    /// <summary>지금 한 묶음이 나가 있나. 나가 있는 동안 쌓인 것이 다음 묶음이 된다.</summary>
    private bool _sending;

    /// <summary>방금 보낸 묶음. 되돌리기가 이것을 거꾸로 보낸다.</summary>
    private Batch? _undo;

    /// <summary>마지막으로 누른 줄의 자리. Shift 범위의 한쪽 끝이다.</summary>
    private int? _lastToggled;

    /// <summary>직전 <c>mousedown</c> 에서 Shift 가 눌려 있었나.</summary>
    private bool _shift;

    /// <summary>지금 기준. 일괄 요청이 이 둘 중 하나만 채워 간다.</summary>
    private AssignmentScope Scope => ByProject
        ? new AssignmentScope(int.Parse(_picked!.Key, System.Globalization.CultureInfo.InvariantCulture), null)
        : new AssignmentScope(null, _picked!.Key);

    private string ScopeName => _picked?.Name ?? string.Empty;

    /// <summary>보이는 것 중 아직 안 들어간 수. 일괄 단추가 이 숫자를 단다.</summary>
    private int Addable => _right.Count(r => !r.On);

    private int Removable => _right.Count(r => r.On);

    /// <summary>일괄 요청의 기준. 둘 중 하나만 채운다 — 둘 다 채우면 상대 열쇠가 아이디인지 프로젝트 번호인지 알 수 없어 서버가 거절한다.</summary>
    private readonly record struct AssignmentScope(int? PrjRid, string? UserId)
    {
        /// <summary>상대 열쇠 하나를 짝으로 편다.</summary>
        public (int Rid, string UserId) Pair(string otherKey) => PrjRid is int rid
            ? (rid, otherKey)
            : (int.Parse(otherKey, System.Globalization.CultureInfo.InvariantCulture), UserId!);
    }

    /// <summary>되돌리기가 보낼 것. 「보낸 것」이 아니라 「보내기 전 상태」다 — 보낸 것을 뒤집으면 연타가 여러 묶음으로 나뉠 때 마지막 묶음만 되돌아온다.</summary>
    private sealed record Batch(
        AssignmentScope Scope,
        string ScopeName,
        IReadOnlyList<string> Add,
        IReadOnlyList<string> Remove)
    {
        public int Count => Add.Count + Remove.Count;
    }

    /// <summary>찾기 칸. 글자가 바뀔 때마다 그 판만 다시 빚는다 — 왕복은 없다.</summary>
    private string LeftQuery
    {
        get => _leftQuery;
        set
        {
            _leftQuery = value ?? string.Empty;
            BuildLeft();
        }
    }

    private string RightQuery
    {
        get => _rightQuery;
        set
        {
            _rightQuery = value ?? string.Empty;
            _lastToggled = null;
            BuildRight();
        }
    }

    // ── 축에 따라 달라지는 말 ─────────────────────────────

    private bool ByProject => _axis == BoardAxis.Project;

    private string LeftTitle => ByProject ? "프로젝트" : "사람";

    private string LeftSubCaption => ByProject ? "기간" : "아이디";

    private string RightTitleShort => ByProject ? "사람" : "프로젝트";

    private string RightSubCaption => ByProject ? "아이디" : "기간";

    private string LeftCountCaption => ByProject ? "인원" : "참여";

    private string OtherCountCaption => ByProject ? "참여 수" : "참여 인원";

    private string RightSearchHint => ByProject ? "이름 · 아이디로 찾기" : "이름으로 찾기";

    private string PickPrompt => ByProject
        ? "왼쪽에서 프로젝트를 고르면 넣고 뺄 사람이 나옵니다."
        : "왼쪽에서 사람을 고르면 넣고 뺄 프로젝트가 나옵니다.";

    private string RightTitle => _picked is null
        ? (ByProject ? "참여자" : "참여 프로젝트")
        : ByProject ? $"{_picked.Name} 의 참여자" : $"{_picked.Name} 의 참여 프로젝트";

    private string RightHint => _picked is null
        ? string.Empty
        : $"{_right.Count(r => r.On)} / {_right.Count}건";

    // ── 조회 ──────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        _axis = Start;
        await ReloadAsync();
    }

    /// <summary>프로젝트 전부 · 계정 전부 · 걸려 있는 짝을 나란히 읽는다. 셋이면 두 축이 다 나오므로 그 뒤로는 왕복이 없다.</summary>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 아이디를 비우면 `Accepted = false` 로 오는 깃발은 쓰지 않는다 — 짝은 `pairs` 가 알려 준다.
        var projects = Api.AssignmentsAsync(null);
        var accounts = Accounts.GetAsync("portal_account");
        var pairs = Api.ListAsync();

        await Task.WhenAll(projects, accounts, pairs);

        _projects = projects.Result;
        _people = accounts.Result;

        _map.Clear();

        foreach (var pair in pairs.Result)
        {
            _map.Add((pair.PrjRid, pair.UserId));
        }

        Rebuild();

        return _left.Count;
    }, "프로젝트도 계정도 읽지 못했습니다.", "참여 자료를 읽지 못했습니다");

    // ── 판 빚기 ───────────────────────────────────────────

    /// <summary>양쪽을 다시 빚는다. 고른 것은 같은 열쇠로 되찾고, 없으면 첫 줄을 고른다 — 고른 것처럼 보이는데 아무 일도 없는 화면을 막는다.</summary>
    private void Rebuild()
    {
        BuildLeft();

        _picked ??= _left.FirstOrDefault();

        BuildRight();
    }

    private void BuildLeft()
    {
        _left = (ByProject ? ProjectRows() : PersonRows())
            .Where(r => Matches(r, _leftQuery))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();

        // 고른 줄은 매번 새 객체라 같은 열쇠로 다시 잡는다. 걸러져 안 보여도 놓지 않는다 — 찾기를 지우면 그 줄이 돌아온다.
        if (_picked is not null && _left.FirstOrDefault(r => r.Key == _picked.Key) is { } again)
        {
            _picked = again;
        }
    }

    /// <summary>오른쪽을 빚는다. 참여한 것이 위로 온다. 체크할 때는 부르지 않는다 — 방금 켠 줄이 손가락 밑에서 튀어 오르기 때문.</summary>
    private void BuildRight()
    {
        if (_picked is null)
        {
            _right = [];
            return;
        }

        _right = (ByProject ? PersonRows() : ProjectRows())
            .Where(r => Matches(r, _rightQuery))
            .Select(r =>
            {
                r.On = _map.Contains(Scope.Pair(r.Key));
                return r;
            })
            .OrderByDescending(r => r.On)
            .ThenBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    private List<BoardRow> ProjectRows() =>
    [
        .. _projects.Select(p => new BoardRow
        {
            Key = p.PrjRid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Name = p.PrjName ?? $"#{p.PrjRid}",
            Sub = Period(p),
            Count = _map.Count(m => m.Rid == p.PrjRid),
        })
    ];

    private List<BoardRow> PersonRows() =>
    [
        .. _people.Select(a => new BoardRow
        {
            Key = a.Value,
            Name = a.Label,
            Sub = a.Value,
            Count = _map.Count(m => string.Equals(m.UserId, a.Value, StringComparison.Ordinal)),
        })
    ];

    private static string Period(ProjectAssignmentDto p) =>
        (p.PrjSdt, p.PrjEdt) switch
        {
            (null, null) => string.Empty,
            (var s, null) => $"{s:yy.MM.dd} ~",
            (null, var e) => $"~ {e:yy.MM.dd}",
            var (s, e) => $"{s:yy.MM.dd}~{e:yy.MM.dd}",
        };

    private static bool Matches(BoardRow row, string query) =>
        string.IsNullOrWhiteSpace(query)
        || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || row.Sub.Contains(query, StringComparison.OrdinalIgnoreCase);

    // ── 조작 ──────────────────────────────────────────────

    private async Task SetAxis(BoardAxis axis)
    {
        if (_axis == axis)
        {
            return;
        }

        // 보내던 묶음은 옛 기준의 것이라 끝나기를 기다린 뒤 축을 옮긴다. 아니면 그 결과가 새 기준의 줄에 얹힌다.
        await WaitForSendAsync();

        _axis = axis;

        // 기준이 바뀌었으니 고른 것과 찾기를 놓는다 — 들고 가면 프로젝트 번호를 아이디 자리에 쓰게 된다.
        _picked = null;
        _leftQuery = string.Empty;
        _rightQuery = string.Empty;
        _lastToggled = null;
        _undo = null;

        Rebuild();
    }

    private async Task PickAsync(BoardRow? row)
    {
        await WaitForSendAsync();

        _picked = row;
        _rightQuery = string.Empty;
        _lastToggled = null;

        // 되돌리기는 방금 보던 기준의 것이다. 다른 곳으로 옮긴 뒤 살려 두면 지금 안 보이는 곳이 바뀐다.
        _undo = null;

        BuildRight();
    }

    private async Task Drill(BoardRow row)
    {
        await WaitForSendAsync();

        var key = row.Key;

        _axis = ByProject ? BoardAxis.Person : BoardAxis.Project;
        _picked = null;
        _leftQuery = string.Empty;
        _rightQuery = string.Empty;
        _lastToggled = null;
        _undo = null;

        BuildLeft();

        _picked = _left.FirstOrDefault(r => r.Key == key);

        BuildRight();
    }

    /// <summary>참여를 켜고 끈다. 화면은 그 자리에서 바뀌고 전송은 뒤에서 한다. Shift 를 누른 채였으면 지난번 누른 줄부터 여기까지 함께 건다.</summary>
    private Task ToggleAsync(BoardRow row, bool on)
    {
        if (_picked is null)
        {
            return Task.CompletedTask;
        }

        var here = _right.IndexOf(row);
        var rows = TakeShift() && _lastToggled is int from && from >= 0 && here >= 0
            ? _right.GetRange(Math.Min(from, here), Math.Abs(here - from) + 1)
            : [row];

        _lastToggled = here;

        return QueueAsync(rows.Where(r => r.On != on || ReferenceEquals(r, row)), on);
    }

    /// <summary>보이는 것을 통째로 켜거나 끈다. 이미 그 상태인 줄은 보내지 않는다.</summary>
    private Task BulkAsync(bool on) => QueueAsync(_right.Where(r => r.On != on), on);

    /// <summary>방금 보낸 묶음을 거꾸로 한 번 더 보낸다. 되돌린 뒤에는 되돌릴 것이 없다 — 같은 단추로 되돌리기와 다시 하기를 겸하면 지금 어느 쪽인지 알 수 없다.</summary>
    private async Task UndoAsync()
    {
        if (_undo is not { } batch)
        {
            return;
        }

        _undo = null;

        var ok = await SendAsync(batch.Scope, batch.Add, batch.Remove,
            $"{batch.ScopeName} 의 변경 {batch.Count}건을 되돌렸습니다.");

        if (!ok)
        {
            // 못 되돌렸으면 단추를 살려 둔다 — 다시 누를 수 있어야 한다.
            _undo = batch;
            return;
        }

        foreach (var key in batch.Add)
        {
            _map.Add(batch.Scope.Pair(key));
        }

        foreach (var key in batch.Remove)
        {
            _map.Remove(batch.Scope.Pair(key));
        }

        // 짝 집합에서 통째로 다시 빚는다. 되돌릴 줄이 지금 안 보일 수 있어 줄 하나씩은 고칠 수 없다.
        Rebuild();
    }

    // ── 보내기 ────────────────────────────────────────────

    /// <summary>바꿀 줄들을 대기줄에 넣고, 아무도 안 보내고 있으면 보내기를 시작한다. 첫 변경은 곧바로 나가고(모아 두면 화면을 옮길 때 켠 것이 안 들어간다), 보내는 동안 쌓인 것만 묶는다.</summary>
    private Task QueueAsync(IEnumerable<BoardRow> rows, bool on)
    {
        if (_picked is null)
        {
            return Task.CompletedTask;
        }

        // 여기서 기다리면 안 된다 — 연타한 두 번째 클릭이 첫 번째가 끝날 때까지 서 있다가 따로 나가 묶음이 무력해진다. 기준 옮기기 셋(PickAsync · SetAxis · Drill)은 보내던 것을 기다린 뒤 옮긴다.
        var scope = Scope;
        var queued = false;

        foreach (var row in rows)
        {
            if (row.On == on)
            {
                continue;
            }

            // 이번 단위에서 처음 손대는 줄이면 그 전 상태를 적는다. 되돌아갈 자리는 이 값이다.
            if (!_before.ContainsKey(row.Key))
            {
                _before[row.Key] = row.On;
            }

            Apply(row, on);
            row.Saving = true;
            _queue[row.Key] = on;
            queued = true;
        }

        if (!queued || _sending)
        {
            return Task.CompletedTask;
        }

        return PumpAsync(scope);
    }

    /// <summary>대기줄이 빌 때까지 묶어 보낸다.</summary>
    private async Task PumpAsync(AssignmentScope scope)
    {
        _sending = true;

        try
        {
            while (_queue.Count > 0)
            {
                var batch = new Dictionary<string, bool>(_queue, StringComparer.Ordinal);
                _queue.Clear();

                var add = batch.Where(p => p.Value).Select(p => p.Key).ToList();
                var remove = batch.Where(p => !p.Value).Select(p => p.Key).ToList();

                var ok = await SendAsync(scope, add, remove, Said(scope, add.Count, remove.Count));

                foreach (var key in batch.Keys)
                {
                    if (_right.FirstOrDefault(r => r.Key == key) is { } row)
                    {
                        row.Saving = false;
                    }
                }

                if (!ok)
                {
                    Revert(scope, batch);

                    // 못 보낸 것은 되돌릴 것도 아니다 — 화면이 이미 제자리다.
                    foreach (var key in batch.Keys)
                    {
                        _before.Remove(key);
                    }

                    // 남은 대기줄도 버린다. 같은 기준에 이어 보내면 화면과 DB 가 더 벌어진다. 다시 읽어야 하는 상태라 여기서 멈춘다.
                    _queue.Clear();
                    break;
                }
            }
        }
        finally
        {
            _sending = false;

            // 대기줄이 빈 때가 한 단위의 끝이다. 되돌리기는 이 단위 전체를 한 번에 되돌린다.
            _undo = _before.Count == 0
                ? _undo
                : new Batch(
                    scope,
                    ScopeName,
                    [.. _before.Where(p => p.Value).Select(p => p.Key)],
                    [.. _before.Where(p => !p.Value).Select(p => p.Key)]);

            _before.Clear();
        }
    }

    /// <summary>한 묶음을 보낸다. 한 건짜리도 같은 길로 간다 — 갈래를 둘로 두면 한쪽에만 걸리는 버그가 생긴다.</summary>
    private Task<bool> SendAsync(
        AssignmentScope scope, IReadOnlyList<string> add, IReadOnlyList<string> remove, string ok)
        => RunAsync(
            () => Api.SetAssignmentsAsync(scope.PrjRid, scope.UserId, add, remove),
            ok,
            "저장하지 못했습니다");

    private static string Said(AssignmentScope scope, int added, int removed) =>
        (added, removed) switch
        {
            (0, 0) => "바뀐 것이 없습니다.",
            (> 0, 0) => $"{added}건 넣었습니다.",
            (0, > 0) => $"{removed}건 뺐습니다.",
            _ => $"{added}건 넣고 {removed}건 뺐습니다.",
        };

    /// <summary>보낸 것이 실패했다. 화면을 보내기 전으로 되돌린다.</summary>
    private void Revert(AssignmentScope scope, Dictionary<string, bool> batch)
    {
        foreach (var (key, on) in batch)
        {
            var pair = scope.Pair(key);

            if (on)
            {
                _map.Remove(pair);
            }
            else
            {
                _map.Add(pair);
            }
        }

        Rebuild();
    }

    /// <summary>보내는 중이면 끝날 때까지 기다린다.</summary>
    private async Task WaitForSendAsync()
    {
        while (_sending)
        {
            await Task.Delay(30);
        }
    }

    /// <summary>Shift 를 쓰고 끈다. 한 번 본 것을 다음 클릭까지 들고 있지 않는다.</summary>
    private bool TakeShift()
    {
        var shift = _shift;
        _shift = false;

        return shift;
    }

    /// <summary>짝 하나를 켜고 끈 결과를 화면이 들고 있는 것 전부에 옮긴다 — 짝 집합 · 오른쪽 줄 · 양쪽의 수. 다시 읽지 않는다.</summary>
    private void Apply(BoardRow row, bool on)
    {
        var pair = Scope.Pair(row.Key);

        if (on)
        {
            _map.Add(pair);
        }
        else
        {
            _map.Remove(pair);
        }

        var delta = on ? 1 : -1;

        row.On = on;
        row.Count += delta;

        if (_picked is not null)
        {
            _picked.Count += delta;
        }
    }
}
