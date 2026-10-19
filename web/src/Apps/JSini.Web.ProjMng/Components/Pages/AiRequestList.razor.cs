using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiRequestList
{
    [Inject] private AiTaskClient Api { get; set; } = default!;

    /// <summary>처음 깔리는 줄 수이자 「더보기」가 한 번에 늘리는 수. 휴대폰 한 화면에 목록 판이 통째로 들어가는 수로 잡았다.</summary>
    private const int RecentMax = 5;

    /// <summary>올려 둔 것 전부. 서버가 내 것만 주고 잘라 주지 않으므로 한 번에 다 온다 — 「더보기」는 몇 줄을 보이느냐의 문제다. 최근이 앞이라(task_key DESC) 앞에서 자르면 최근 몇 건이 된다.</summary>
    private IReadOnlyList<AiTaskDto> _rows = [];

    /// <summary>「더보기」를 몇 번 눌렀나. 다시 읽어도 유지된다 — 고쳐 저장하면 목록을 다시 읽는데, 접히면 보던 줄이 눈앞에서 사라진다.</summary>
    private int _more;

    /// <summary>지금 고치는 중인 건. null 이면 새로 적는 중이다. 목록에서 집은 자료를 그대로 붙든다 — 이 화면은 「새로고침」을 누를 때만 목록을 읽는다.</summary>
    private AiTaskDto? _editing;

    private string? _title;
    private string? _text;

    private ConfirmDialog? _confirm;

    /// <summary>남긴 말 판. 손에 쥐는 이유는 하나 — 「새로고침」 때 고른 건이 같으면 부품이 다시 읽지 않으므로 여기서 직접 부른다.</summary>
    private AiTaskNotes? _notes;

    /// <summary>앱푸시를 누르고 들어온 사람이 실어 오는 번호(?task=123). 알림이 이 주소를 싣고, 목록만 열면 말이 붙은 줄을 다시 찾아야 한다. 이름을 Task 로 두지 않는다 — Task 를 가리면 async 메서드가 안 컴파일된다.</summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "task")]
    public long? TaskKey { get; set; }

    /// <summary>주소로 실려 온 번호를 이미 한 번 열었나. 한 번만 연다 — 「고치기 취소」로 닫은 사람에게 다시 열면 닫을 길이 없다.</summary>
    private bool _opened;

    /// <summary>글상자의 id. label 이 for 로 이것을 가리켜, 칸 이름을 눌러도 커서가 가고 읽기 도구가 이름을 말한다.</summary>
    private readonly string _domId = $"pm-req-{Guid.NewGuid():N}";

    private string TitleId => $"{_domId}-title";
    private string TextId => $"{_domId}-text";

    /// <summary>고르긴 했는데 더 고칠 수 없는 건인가. 판정은 AiRequestStage.Editable 하나이고 서버도 같은 것을 본다.</summary>
    private bool ReadOnly => _editing is not null && !AiRequestStage.Editable(_editing);

    private bool CanSave => !Loading && !ReadOnly && !string.IsNullOrWhiteSpace(_text);

    private string SaveText => _editing is null ? "요청 올리기" : "고친 내용 저장";

    private string WriteTitle => _editing is null ? "새 요청" : "요청 고치기";

    /// <summary>판 머리의 한 줄. 이 화면이 무엇을 안 하는지 말한다 — 「저장했는데 왜 안 돌지」가 가장 자주 나오는 물음이다.</summary>
    private string WriteHint => ReadOnly
        ? "이미 처리가 시작된 요청입니다. 내용만 볼 수 있습니다."
        : "적어서 올려 두면 관리자가 내용을 확인하고 작업 위치와 AI 를 정해 실행합니다.";

    /// <summary>지금 깔리는 줄. 앞에서부터 자른다 — 목록이 최근 순이라 곧 최근 몇 건이다.</summary>
    private IEnumerable<AiTaskDto> Shown => _rows.Take(ShownCount);

    private int ShownCount => Math.Min(_rows.Count, RecentMax * (_more + 1));

    /// <summary>잘려 나가 안 보이는 줄 수. 「더보기」 단추에 적힌다.</summary>
    private int Rest => _rows.Count - ShownCount;

    private string ListHint => _rows.Count == 0
        ? string.Empty
        : Rest > 0
            ? $"전체 {_rows.Count}건 중 최근 {ShownCount}건"
            : $"{_rows.Count}건";

    /// <summary>자른 자리를 <see cref="RecentMax"/> 줄만큼 늘린다.</summary>
    private void ShowMore() => _more++;

    /// <summary>처음 다섯 줄로 되돌린다.</summary>
    private void Fold() => _more = 0;

    protected override Task OnInitializedAsync() => LoadMineAsync();

    /// <summary>「새로고침」. 목록과 함께 남긴 말도 다시 읽는다 — 고른 건이 같으면 그 부품은 스스로 다시 읽지 않는다.</summary>
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

        // 앱푸시로 들어왔다. 목록을 읽은 뒤에만 연다 — 번호만 따로 받는 길이 없고, 열 건은 방금 받은 목록 안에 있다.
        if (!_opened && TaskKey is { } wanted)
        {
            _opened = true;

            if (_rows.FirstOrDefault(r => r.TaskKey == wanted) is { } found)
            {
                Pick(found);
            }
            else
            {
                // 그 사이 거둬들였거나 남의 건이다. 말없이 넘기지 않는다 — 알림을 눌렀는데 아무 일도 없으면 고장으로 읽힌다.
                Say("그 요청을 찾지 못했습니다. 거둬들였거나 다른 사람의 것입니다.",
                    NoticeTone.Warning);
            }
        }

        // 고치던 건이 그 사이 관리자에게 넘어갔을 수 있다. 옛 자료를 그대로 두면 읽기 전용이어야 할 건에 저장 단추가 남는다.
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

    /// <summary>그 줄이 잘리는 자리 밖이면 보일 때까지 늘린다. 고치는 중인 건이 목록에서 안 보이는 자리가 되지 않게 한다.</summary>
    private void Reveal(AiTaskDto t)
    {
        var at = _rows.ToList().FindIndex(r => r.TaskKey == t.TaskKey);

        if (at < 0)
        {
            return;
        }

        // at 은 0 부터이므로 at + 1 번째 줄이다. 그 줄이 들어갈 때까지 한 뭉치를 몇 번 늘려야 하는지가 _more 다.
        _more = Math.Max(_more, at / RecentMax);
    }

    /// <summary>목록에서 하나를 집었다. 위 판에 싣는다.</summary>
    private void Pick(AiTaskDto t)
    {
        // 같은 것을 또 누르면 접는다 — 되돌아갈 길이 「고치기 취소」뿐이면 목록만 보던 사람이 갇힌다.
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

    /// <summary>적은 것을 올린다. 새로 올리기와 고치기가 한 단추다. 「빠른 지시」와 달리 요청까지 잇지 않는다 — 다음 걸음은 관리자의 것이다.</summary>
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
