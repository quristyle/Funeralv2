using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiRequestList
{
    [Inject] private AiTaskClient Api { get; set; } = default!;

    /// <summary>
    /// 처음 깔리는 줄 수이자 <b>「더보기」가 한 번에 늘리는 수</b>다.
    /// 휴대폰 한 화면에 목록 판이 통째로 들어가는 수로 잡았다 — 위 머리말 참고.
    /// </summary>
    private const int RecentMax = 5;

    /// <summary>
    /// 올려 둔 것 <b>전부</b>. <b>서버가 내 것만 준다.</b>
    /// 서버가 잘라 주지 않으므로 한 번에 다 온다 — 「더보기」는 여기서 몇 줄을
    /// 꺼내 보이느냐의 문제다(<see cref="Shown"/>).
    /// </summary>
    /// <remarks>
    /// <b>최근이 앞이다</b>(<c>ORDER BY task_key DESC</c>). 앞에서 자르는 것이
    /// 곧 「최근 몇 건」인 것은 그 정렬에 기댄 것이다.
    /// </remarks>
    private IReadOnlyList<AiTaskDto> _rows = [];

    /// <summary>
    /// 「더보기」를 몇 번 눌렀나. 한 번에 <see cref="RecentMax"/> 줄씩 더 꺼낸다.
    /// <b>다시 읽어도 유지된다</b> — 고쳐 저장하면 목록을 다시 읽는데, 그때
    /// 접히면 펼쳐 놓고 보던 줄이 눈앞에서 사라진다.
    /// </summary>
    private int _more;

    /// <summary>
    /// 지금 고치는 중인 건. <c>null</c> 이면 새로 적는 중이다.
    /// </summary>
    /// <remarks>
    /// <b>목록에서 집은 자료를 그대로 붙든다.</b> 번호만 들고 목록에서 다시
    /// 찾는 방식(「빠른 지시」의 창이 그렇다)은 목록이 몇 초마다 새로 읽힐 때
    /// 필요한 것인데, 이 화면은 사람이 「새로고침」을 누를 때만 읽는다.
    /// </remarks>
    private AiTaskDto? _editing;

    private string? _title;
    private string? _text;

    private ConfirmDialog? _confirm;

    /// <summary>
    /// 남긴 말 판. <b>손에 쥐는 이유는 하나뿐이다</b> — 「새로고침」을 눌렀을
    /// 때 고른 건이 그대로면 그 부품은 번호로 걸러 다시 읽지 않으므로,
    /// 여기서 직접 불러 준다.
    /// </summary>
    private AiTaskNotes? _notes;

    /// <summary>
    /// <b>앱푸시를 누르고 들어온 사람이 실어 오는 번호</b>(<c>?task=123</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 관리자가 남긴 말의 알림이 이 주소를 싣는다
    /// (<c>AiRequestAlerter.RequestUrl</c>). 목록만 열어 주면 알림을 누른
    /// 사람이 <b>어느 줄에 말이 붙었는지 눈으로 다시 찾아야</b> 한다.
    /// </para>
    /// <para>
    /// 이름을 <c>Task</c> 로 두지 않는다 — 화면 안에서 <c>Task</c> 는
    /// <see cref="System.Threading.Tasks.Task"/> 라, 가리면 <c>async</c>
    /// 메서드가 통째로 안 컴파일된다.
    /// </para>
    /// </remarks>
    [Parameter]
    [SupplyParameterFromQuery(Name = "task")]
    public long? TaskKey { get; set; }

    /// <summary>
    /// 주소로 실려 온 번호를 <b>이미 한 번 열었나</b>. 한 번만 연다 —
    /// 열어 놓고 「고치기 취소」로 닫은 사람에게 다시 열어 주면 닫을 길이 없다.
    /// </summary>
    private bool _opened;

    /// <summary>
    /// 글상자의 id. <c>label</c> 이 <c>for</c> 로 이것을 가리킨다 —
    /// 칸 이름을 눌러도 칸에 커서가 가고, 읽어 주는 도구가 이름을 말할 수 있다.
    /// </summary>
    private readonly string _domId = $"pm-req-{Guid.NewGuid():N}";

    private string TitleId => $"{_domId}-title";
    private string TextId => $"{_domId}-text";

    /// <summary>
    /// 고르긴 했는데 <b>더 고칠 수 없는</b> 건인가. 판정은
    /// <see cref="AiRequestStage.Editable"/> 하나이고 서버도 같은 것을 본다.
    /// </summary>
    private bool ReadOnly => _editing is not null && !AiRequestStage.Editable(_editing);

    private bool CanSave => !Loading && !ReadOnly && !string.IsNullOrWhiteSpace(_text);

    private string SaveText => _editing is null ? "요청 올리기" : "고친 내용 저장";

    private string WriteTitle => _editing is null ? "새 요청" : "요청 고치기";

    /// <summary>
    /// 판 머리의 한 줄. <b>이 화면이 무엇을 안 하는지</b>를 여기서 말한다 —
    /// 「저장했는데 왜 안 돌지」가 이 화면에서 가장 나오기 쉬운 물음이다.
    /// </summary>
    private string WriteHint => ReadOnly
        ? "이미 처리가 시작된 요청입니다. 내용만 볼 수 있습니다."
        : "적어서 올려 두면 관리자가 내용을 확인하고 작업 위치와 AI 를 정해 실행합니다.";

    /// <summary>
    /// 지금 깔리는 줄. <b>앞에서부터 자른다</b> — 목록이 최근 순이라 그것이
    /// 곧 「최근 몇 건」이다.
    /// </summary>
    private IEnumerable<AiTaskDto> Shown => _rows.Take(ShownCount);

    private int ShownCount => Math.Min(_rows.Count, RecentMax * (_more + 1));

    /// <summary>잘려 나가 안 보이는 줄 수. 「더보기」 단추에 적힌다.</summary>
    private int Rest => _rows.Count - ShownCount;

    private string ListHint => _rows.Count == 0
        ? string.Empty
        : Rest > 0
            ? $"전체 {_rows.Count}건 중 최근 {ShownCount}건 · 「접수 대기」인 것만 고치거나 거둬들일 수 있습니다."
            : $"{_rows.Count}건 · 「접수 대기」인 것만 고치거나 거둬들일 수 있습니다.";

    /// <summary>자른 자리를 <see cref="RecentMax"/> 줄만큼 늘린다.</summary>
    private void ShowMore() => _more++;

    /// <summary>처음 다섯 줄로 되돌린다.</summary>
    private void Fold() => _more = 0;

    protected override Task OnInitializedAsync() => LoadMineAsync();

    /// <summary>
    /// 「새로고침」. 목록과 함께 <b>남긴 말도 다시 읽는다</b> — 관리자가 그
    /// 사이에 답을 달았는지가 이 화면에서 가장 알고 싶은 것이고, 고른 건이
    /// 그대로면 그 부품은 스스로 다시 읽지 않는다.
    /// </summary>
    private async Task RefreshAsync()
    {
        await LoadMineAsync();

        if (_notes is not null)
        {
            await _notes.ReloadAsync();
        }
    }

    private async Task LoadMineAsync()
    {
        await LoadAsync(async () =>
        {
            _rows = await Api.MineAsync();
            return _rows.Count;
        }, emptyMessage: string.Empty, failMessage: "올린 요청을 읽지 못했습니다");

        // 앱푸시를 누르고 들어왔다. **목록을 읽은 뒤라야 연다** — 이 화면은
        // 번호 하나만 따로 받아 오는 길이 없고, 열 건은 방금 받은 목록 안에 있다.
        if (!_opened && TaskKey is { } wanted)
        {
            _opened = true;

            if (_rows.FirstOrDefault(r => r.TaskKey == wanted) is { } found)
            {
                Pick(found);
            }
            else
            {
                // 그 사이에 거둬들였거나 남의 건 번호다(서버가 내 것만 준다).
                // **말없이 넘기지 않는다** — 알림을 누르고 들어왔는데 아무
                // 일도 안 일어나면 고장으로 읽힌다.
                Say("그 요청을 찾지 못했습니다. 거둬들였거나 다른 사람의 것입니다.",
                    NoticeTone.Warning);
            }
        }

        // 고치던 건이 그 사이에 관리자에게 넘어갔을 수 있다. **손에 쥔 옛
        // 자료를 그대로 두면** 읽기 전용이어야 할 건에 저장 단추가 남는다.
        if (_editing is { } picked)
        {
            _editing = _rows.FirstOrDefault(r => r.TaskKey == picked.TaskKey);

            if (_editing is null)
            {
                ResetForm();
            }
            else
            {
                Reveal(_editing);
            }
        }
    }

    /// <summary>
    /// 그 줄이 <b>잘리는 자리 밖이면 보일 때까지 늘린다.</b>
    /// </summary>
    /// <remarks>
    /// 위 판은 「고치는 중」인데 목록에는 그 줄이 없는 자리가 되지 않게 한다 —
    /// 어느 것을 고치는 중인지는 목록의 <c>pm-req__head--picked</c> 가 말한다.
    /// </remarks>
    private void Reveal(AiTaskDto t)
    {
        var at = _rows.ToList().FindIndex(r => r.TaskKey == t.TaskKey);

        if (at < 0)
        {
            return;
        }

        // at 이 0부터이므로 at + 1 번째 줄이다. 그 줄이 들어가려면 한 뭉치를
        // 몇 번 늘려야 하는지가 그대로 `_more` 다.
        _more = Math.Max(_more, at / RecentMax);
    }

    /// <summary>목록에서 하나를 집었다. 위 판에 싣는다.</summary>
    private void Pick(AiTaskDto t)
    {
        // 같은 것을 또 누르면 접는다 — 새로 적는 자리로 돌아가는 길이
        // 「고치기 취소」 하나뿐이면 목록만 보던 사람이 갇힌다.
        if (_editing?.TaskKey == t.TaskKey)
        {
            ResetForm();
            return;
        }

        _editing = t;
        _title = t.Title;
        _text = t.Contents;
    }

    private void ResetForm()
    {
        _editing = null;
        _title = null;
        _text = null;
    }

    /// <summary>
    /// 적은 것을 올린다. <b>새로 올리기와 고치기가 한 단추다.</b>
    /// </summary>
    /// <remarks>
    /// <b>「빠른 지시」처럼 요청까지 잇지 않는다.</b> 이 화면의 전제가
    /// 「저장만 해 둔다」이고, 그 다음 걸음은 관리자의 것이다.
    /// </remarks>
    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        var ok = await RunAsync(async () =>
        {
            if (_editing is { } current)
            {
                await Api.UpdateMineAsync(new AiTaskDto
                {
                    TaskKey = current.TaskKey,
                    Title = _title,
                    Contents = _text,
                    RowVersion = current.RowVersion,
                });
            }
            else
            {
                await Api.CreateMineAsync(new AiTaskDto
                {
                    Title = _title,
                    Contents = _text,
                    ContentFormat = "markdown",
                });
            }
        },
        okMessage: _editing is null ? "요청을 올렸습니다." : "고친 내용을 저장했습니다.",
        failMessage: "저장하지 못했습니다");

        if (!ok)
        {
            return;
        }

        ResetForm();
        await LoadMineAsync();
    }

    /// <summary>올려 둔 것을 거둬들인다. 아직 아무도 안 건드린 것만.</summary>
    private async Task DeleteAsync()
    {
        if (_editing is not { } current || _confirm is null)
        {
            return;
        }

        var yes = await _confirm.AskAsync(
            $"「{current.Title}」 요청을 거둬들입니다.\n\n다시 되돌릴 수 없습니다.",
            title: "요청 거둬들이기",
            confirmText: "거둬들이기");

        if (!yes)
        {
            return;
        }

        var ok = await RunAsync(
            () => Api.DeleteMineAsync(current.TaskKey),
            okMessage: "거둬들였습니다.",
            failMessage: "거둬들이지 못했습니다");

        if (!ok)
        {
            return;
        }

        ResetForm();
        await LoadMineAsync();
    }
}
