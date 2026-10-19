using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class AiTaskNotes
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private UserFaceClient Faces { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>말이 붙을 요청. <c>null</c> 이면 판이 통째로 빠진다.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>올린 사람의 자리인가. 참이면 mine 경로로 읽고, 펴는 순간 읽은 것으로 찍는다.</summary>
    [Parameter] public bool Mine { get; set; }

    /// <summary>안 읽은 말을 읽은 것으로 찍으면 부모에게 알린다. 목록 배지를 끄는 것은 부모의 일이다.</summary>
    [Parameter] public EventCallback OnRead { get; set; }

    /// <summary>말이 하나 붙었다. 부모가 건수를 다시 읽는다.</summary>
    [Parameter] public EventCallback OnAdded { get; set; }

    private IReadOnlyList<AiTaskNoteDto> _rows = [];

    private string? _text;

    /// <summary>저장 중. 두 번 눌러 같은 말이 두 줄 붙는 것을 막는다.</summary>
    private bool _busy;

    /// <summary>지금 읽어 둔 건. <b>부모가 다른 건을 넘겼는지</b> 보는 값이다.</summary>
    private long _shown;

    /// <summary>서버 한도(AiTaskNoteService.MaxLength)와 같은 수다. 갈라지면 저장을 눌러야만 알게 된다.</summary>
    private const int MaxLength = 2000;

    private bool CanSave => !_busy
                            && Item is not null
                            && !string.IsNullOrWhiteSpace(_text);

    private string EmptyText => Mine
        ? "아직 오간 말이 없습니다. 덧붙일 말이 있으면 아래에 적어 두십시오."
        : "아직 오간 말이 없습니다.";

    private string WriteHint => Mine
        ? "관리자에게 덧붙일 말을 적으십시오. 남기면 관리자에게 알림이 갑니다."
        : "요청한 사람에게 남길 말을 적으십시오. 남기면 그 사람에게 알림이 갑니다.";

    /// <summary>적은 사람의 이름. 아직 얼굴을 못 읽었으면 아이디다.</summary>
    private string Who(string? loginId) => Faces.Get(loginId)?.Name ?? loginId ?? "-";

    protected override async Task OnParametersSetAsync()
    {
        var key = Item?.TaskKey ?? 0;

        // 부모가 같은 건을 몇 초마다 다시 넘긴다. 번호가 같으면 다시 읽지 않는다 — 읽으면 쓰던 글상자가 흔들린다.
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        _text = null;
        _rows = [];

        if (key == 0)
        {
            return;
        }

        await LoadAsync(key);
    }

    private async Task LoadAsync(long taskKey)
    {
        try
        {
            _rows = Mine
                ? await Api.MineNotesAsync(taskKey)
                : await Api.NotesAsync(taskKey);
        }
        catch (ApiException ex)
        {
            // 판을 통째로 죽이지 않는다. 지시와 결과는 그대로 읽혀야 한다.
            _rows = [];
            Toasts.Show($"남긴 말을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }

        // 얼굴은 모아서 한 번에 묻는다 — 줄마다 묻으면 게이트웨이를 여러 번 두드린다.
        await Faces.EnsureAsync(_rows.Select(r => r.CreId));

        await MarkReadAsync(taskKey);
    }

    /// <summary>펴는 순간 읽은 것으로 찍는다. 올린 사람의 자리에서만, 안 읽은 것이 있을 때만 부른다.</summary>
    private async Task MarkReadAsync(long taskKey)
    {
        if (!Mine || !_rows.Any(r => !r.IsOwner && r.ReadDt is null))
        {
            return;
        }

        try
        {
            await Api.ReadMineNotesAsync(taskKey);
        }
        catch (ApiException)
        {
            // 말없이 넘긴다 — 읽음 표시가 늦을 뿐, 사람이 할 일 없는 실패에 경고를 띄우지 않는다.
            return;
        }

        // 손에 쥔 것도 고친다 — 다시 읽지 않으므로 안 고치면 안 읽음 딱지가 남는다.
        foreach (var row in _rows.Where(r => !r.IsOwner && r.ReadDt is null))
        {
            row.ReadDt = AppTime.UtcNow;
        }

        await OnRead.InvokeAsync();
    }

    private async Task SaveAsync()
    {
        if (!CanSave || Item is not { } t)
        {
            return;
        }

        var text = (_text ?? string.Empty).Trim();

        if (text.Length > MaxLength)
        {
            Toasts.Show($"남길 말은 {MaxLength}자까지 적을 수 있습니다.", NoticeTone.Warning);
            return;
        }

        _busy = true;

        try
        {
            if (Mine)
            {
                await Api.AddMineNoteAsync(t.TaskKey, text);
            }
            else
            {
                await Api.AddNoteAsync(t.TaskKey, text);
            }
        }
        catch (ApiException ex)
        {
            Toasts.Show($"남기지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }
        finally
        {
            _busy = false;
        }

        _text = null;
        Toasts.Show("남겼습니다. 상대에게 알림이 갑니다.");

        // 서버에서 다시 읽는다 — 손으로 끼우면 그 사이 상대가 남긴 줄이 빠진다.
        await LoadAsync(t.TaskKey);
        await OnAdded.InvokeAsync();
    }

    /// <summary>부모가 목록을 새로 읽어 같은 건을 넘길 때 쓴다. OnParametersSetAsync 는 번호로 걸러 내기 때문이다.</summary>
    public async Task ReloadAsync()
    {
        if (Item is { } t)
        {
            await LoadAsync(t.TaskKey);
            StateHasChanged();
        }
    }
}
