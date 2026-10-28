using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ScheduleNote : IDisposable
{
    [Inject] private WbsClient Api { get; set; } = default!;

    [Inject] private ScheduleNoteAssist Assist { get; set; } = default!;

    /// <summary>떠날 때 쓰던 모습을 맡기는 곳. 돌아오면 그대로 되찾는다.</summary>
    [Inject] private ScreenState Screen { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Inject] private CurrentUser Me { get; set; } = default!;

    /// <summary>이 화면이 맡겨 두는 짐의 이름.</summary>
    internal const string StateKey = "projmng/proj/schedule-note";

    /// <summary>사용한 프로젝트를 브라우저에 적어 둘 때 쓰는 저장소 열쇠 앞부분.</summary>
    internal const string ProjectStorageKeyPrefix = "jsini-snote-prj:";

    /// <summary>현재 로그인한 사용자별 저장소 열쇠.</summary>
    private string ProjectStorageKey =>
        $"{ProjectStorageKeyPrefix}{(string.IsNullOrWhiteSpace(Me?.Username) ? "?" : Me.Username.Trim())}";

    /// <summary>로그인한 사용자의 성명. 없으면 아이디.</summary>
    private string? CurrentUserName =>
        string.IsNullOrWhiteSpace(Me?.DisplayName)
            ? (string.IsNullOrWhiteSpace(Me?.Username) ? null : Me.Username.Trim())
            : Me.DisplayName.Trim();

    /// <summary>떠날 때 챙기는 입력 내용과 상태. 다른 화면에 갔다 돌아와도 그대로 살린다.</summary>
    private sealed record Kept(
        string? ProjectCode,
        string? Kind,
        DateOnly? Start,
        DateOnly? End,
        string? Title,
        string? Summary,
        string? Body,
        IReadOnlyCollection<string>? Touched,
        WbsItemDto? Saved,
        string? Model,
        string? Failed);

    /// <summary>치기를 멈추고 이만큼 지나야 AI 에게 묻는다. 글자마다 물으면 공급자 한도(429)에 먼저 닿는다.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1200);

    /// <summary>구분을 안 고르면 쓴다. 비우면 일정표의 어느 구분 거르기에도 안 걸리는 줄이 된다.</summary>
    private const string DefaultKind = "Public";

    private string? _projectCode;

    /// <summary>
    /// 고른 프로젝트. 바꾸면 브라우저 저장소에 적어 두어 다음 접속 때도 같은 프로젝트로 연다.
    /// </summary>
    private string? ProjectCode
    {
        get => _projectCode;
        set
        {
            if (string.Equals(_projectCode, value, StringComparison.Ordinal)) return;

            _projectCode = value;
            if (_storageReady && !string.IsNullOrWhiteSpace(value))
            {
                _ = RememberProjectAsync(value);
            }
        }
    }

    /// <summary>브라우저 저장소를 읽을 준비가 되었는가. 첫 복원 전에 들어오는 값은 기억을 덮지 않는다.</summary>
    private bool _storageReady;

    /// <summary>화면을 떠났다 돌아왔을 때 ScreenState 에서 되찾았는가.</summary>
    private bool _fromScreenState;

    private string? _kind = DefaultKind;
    private DateOnly? _start;
    private DateOnly? _end;
    private string? _title;
    private string? _summary;
    private string? _body;

    /// <summary>사람이 손으로 고친 칸. AI 가 그 칸은 덮지 않는다.</summary>
    private readonly HashSet<string> _touched = [];

    /// <summary>한 번 저장한 줄. 그 뒤의 저장은 새 줄이 아니라 이 줄을 고친다 — 안 그러면 누를 때마다 일정이 하나씩 는다.</summary>
    private WbsItemDto? _saved;

    private int? SavedId => _saved?.WbsId;

    private CancellationTokenSource? _pending;

    /// <summary>나간 물음의 차례. 늦게 온 옛 답이 새 답을 덮지 않게 한다.</summary>
    private int _ask;

    private bool _thinking;
    private string? _model;
    private string? _failed;

    private string? AiState =>
        _thinking ? "AI 가 정리하는 중…"
        : _failed is not null ? _failed
        : _model is not null ? $"AI · {_model}"
        : null;

    private void OnBodyChanged(string? text)
    {
        _body = text;
        _ = AskLaterAsync(force: false, delay: Settle);
    }

    /// <summary>사람이 고친 칸을 무시하고 본문으로 다시 채운다.</summary>
    private Task RefillAsync()
    {
        _touched.Clear();
        return AskLaterAsync(force: true, delay: TimeSpan.Zero);
    }

    private async Task AskLaterAsync(bool force, TimeSpan delay)
    {
        _pending?.Cancel();
        _pending?.Dispose();

        var cts = _pending = new CancellationTokenSource();
        var ask = ++_ask;
        var body = _body;

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cts.Token);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            _thinking = true;
            _failed = null;
            await InvokeAsync(StateHasChanged);

            var draft = await Assist.DraftAsync(body, CurrentUserName, cts.Token);

            if (ask != _ask)
            {
                return;
            }

            _model = draft.Model ?? _model;

            if (draft is { Start: null, Title: null, Summary: null })
            {
                _failed = "AI 답을 읽지 못했습니다";
            }
            else
            {
                Apply(draft, force);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            if (ask == _ask)
            {
                _failed = "AI 에 닿지 못했습니다";
            }
        }
        finally
        {
            if (ask == _ask)
            {
                _thinking = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void Apply(ScheduleDraft draft, bool force)
    {
        if (draft.Start is not null && (force || !_touched.Contains(nameof(_start)))) _start = draft.Start;
        if (draft.End is not null && (force || !_touched.Contains(nameof(_end)))) _end = draft.End;
        if (draft.Title is not null && (force || !_touched.Contains(nameof(_title)))) _title = draft.Title;
        if (draft.Summary is not null && (force || !_touched.Contains(nameof(_summary)))) _summary = draft.Summary;
    }

    /// <summary>사람이 왼쪽 칸을 고쳤다. 고친 칸으로 적어 두어 다음 AI 답이 덮지 않게 한다.</summary>
    private void Touch(string field, Action set)
    {
        set();
        _touched.Add(field);
    }

    private async Task SaveAsync()
    {
        if (!int.TryParse(_projectCode, out var prjRid))
        {
            Say("프로젝트를 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_title))
        {
            Say("제목을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var start = _start ?? AppTime.Today;
        var end = _end is { } e && e >= start ? e : start;

        // 고칠 때는 저장해 둔 줄 위에 얹는다. 서버의 수정은 칸 전부를 덮으므로 새 객체를 보내면 담당·개발일 같은 칸이 비워진다.
        var item = _saved ?? new WbsItemDto();

        item.PrjRid = prjRid;
        item.ScheduleType = string.IsNullOrWhiteSpace(_kind) ? DefaultKind : _kind;
        item.ProcNm = _title.Trim();
        item.PlanSdt = start.ToDateTime(TimeOnly.MinValue);
        item.PlanEdt = end.ToDateTime(TimeOnly.MinValue);
        item.Summary = _summary?.Trim() ?? string.Empty;
        item.Comm = _summary?.Trim() ?? string.Empty;
        item.BodyText = _body ?? string.Empty;
        if (string.IsNullOrWhiteSpace(item.DevUser))
        {
            item.DevUser = CurrentUserName;
        }

        var isNew = _saved is null;
        WbsItemDto? saved = null;

        if (await RunAsync(
                async () => saved = isNew ? await Api.CreateAsync(item) : await Api.UpdateAsync(item),
                isNew ? "일정에 저장했습니다." : "일정을 고쳤습니다.",
                "일정을 저장하지 못했습니다")
            && saved is not null)
        {
            _saved = saved;
            _start = saved.PlanSdt.HasValue ? DateOnly.FromDateTime(saved.PlanSdt.Value) : null;
            _end = saved.PlanEdt.HasValue ? DateOnly.FromDateTime(saved.PlanEdt.Value) : null;
            _ = RememberProjectAsync(_projectCode);
        }
    }

    /// <summary>되찾기는 동기(OnInitialized)에서 한다.</summary>
    protected override void OnInitialized()
    {
        if (Screen.Get<Kept>(StateKey) is not { } kept) return;

        _projectCode = kept.ProjectCode;
        _fromScreenState = !string.IsNullOrWhiteSpace(_projectCode);
        _kind = kept.Kind;
        _start = kept.Start;
        _end = kept.End;
        _title = kept.Title;
        _summary = kept.Summary;
        _body = kept.Body;
        _touched.Clear();
        if (kept.Touched is not null)
        {
            foreach (var t in kept.Touched)
            {
                _touched.Add(t);
            }
        }
        _saved = kept.Saved;
        _model = kept.Model;
        _failed = kept.Failed;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            if (!_fromScreenState)
            {
                var remembered = await ReadRememberedProjectAsync();
                if (!string.IsNullOrWhiteSpace(remembered) && !string.Equals(_projectCode, remembered, StringComparison.Ordinal))
                {
                    _projectCode = remembered;
                    StateHasChanged();
                }
            }
        }
        finally
        {
            _storageReady = true;
        }
    }

    /// <summary>브라우저 저장소에서 마지막으로 사용한 프로젝트를 읽는다.</summary>
    private async Task<string?> ReadRememberedProjectAsync()
    {
        try
        {
            return await JS.InvokeAsync<string?>("localStorage.getItem", ProjectStorageKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>사용한 프로젝트를 브라우저 저장소에 적어 둔다.</summary>
    private async Task RememberProjectAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", ProjectStorageKey, code);
        }
        catch (JSDisconnectedException)
        {
            // 창이 닫혔다.
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // 저장소가 막혔거나 꽉 찼다.
        }
    }

    private void Reset()
    {
        _pending?.Cancel();
        _ask++;

        _saved = null;
        _start = _end = null;
        _title = _summary = _body = null;
        _touched.Clear();
        _thinking = false;
        _failed = null;
        Screen.Clear(StateKey);
    }

    private static DateTime? Day(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? Only(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    public void Dispose()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        Screen.Set(StateKey, new Kept(
            _projectCode,
            _kind,
            _start,
            _end,
            _title,
            _summary,
            _body,
            _touched.ToArray(),
            _saved,
            _model,
            _failed));
    }
}
