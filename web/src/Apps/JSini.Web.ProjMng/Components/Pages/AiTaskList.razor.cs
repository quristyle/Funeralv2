using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using System.Text.Json;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiTaskList
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private AiTargetClient TargetApi { get; set; } = default!;
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;
    [Inject] private AiTaskDraftStore Drafts { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>AI 고르개에 한도를 적는 자리. 대시보드가 아니라 한도 표 하나만 읽는다.</summary>
    [Inject] private AiDashboardClient Dashboard { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(StatusFilters, o => o.Value, o => o.Text, _status),
        SchSummary.NameOf(FlagFilters, o => o.Value, o => o.Text, _flag),
        _targets.FirstOrDefault(t => t.TargetKey == _targetKey)?.TargetNm,
        SchSummary.NameOf(SourceFilters, o => o.Value, o => o.Text, _source),
        _keyword,
        SchSummary.On(_excludeDone, "완료 제외"),
        SchSummary.On(_excludeConfirmed, "확인 제외"));

    /// <summary>휴대폰인가. 요약·조회판을 접을지 이 값으로 정한다. 경계 767px 는 projmng.css 와 같아야 한다.</summary>
    private bool _isPhone;

    private void OnPhoneChanged(bool active)
    {
        _isPhone = active;
    }

    private readonly string _swipeListId = $"pm-ai-tasks-{Guid.NewGuid():N}";
    private IJSObjectReference? _swipeModule;
    private DotNetObjectReference<AiTaskList>? _swipeRef;

    private int _mobilePageSize = 15;
    private int _mobilePageCount = 1;

    private IReadOnlyList<AiTaskDto> MobileShown =>
        [.. _rows.Take(_mobilePageCount * _mobilePageSize)];

    private bool HasMoreMobile => MobileShown.Count < _rows.Count;

    private void LoadMoreMobile() => _mobilePageCount++;

    private bool CanConfirmTask(AiTaskDto t) =>
        Can(MenuAction.Update) && !t.IsBusy && !t.UserConfirmed;

    private bool CanDeleteTask(AiTaskDto t) =>
        Can(MenuAction.Delete) && t.TaskKey > 0 && !t.IsBusy;

    private string KindName(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? "-" : RunnerKinds.FirstOrDefault(o => o.Value == kind)?.Text ?? kind;

    private async Task AttachSwipeAsync()
    {
        try
        {
            _swipeModule ??= await Js.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/ask-swipe.js");
            _swipeRef ??= DotNetObjectReference.Create(this);

            await _swipeModule.InvokeVoidAsync("attachSwipe", $"#{_swipeListId}", _swipeRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    /// <summary>모바일 목록에서 오른쪽으로 밀면 확인(완료) 처리한다.</summary>
    [JSInvokable]
    public async Task<bool> ConfirmSwipedAsync(long taskKey)
    {
        var item = _rows.FirstOrDefault(t => t.TaskKey == taskKey);
        if (item is null)
        {
            return false;
        }

        if (!CanConfirmTask(item))
        {
            return true;
        }

        _rows = [.. _rows.Where(t => t.TaskKey != taskKey)];
        if (_picked?.TaskKey == taskKey)
        {
            _picked = null;
            if (_draftAt is null)
            {
                _edit = null;
                _base = null;
            }
        }
        if (_reveal?.TaskKey == taskKey)
        {
            _reveal = null;
        }
        await InvokeAsync(StateHasChanged);

        try
        {
            await Api.ConfirmAsync(taskKey);
            Say("확인 완료 처리했습니다.");
            return false;
        }
        catch (ApiException ex)
        {
            Say($"확인 처리하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            await SearchAsync();
            return false;
        }
    }

    /// <summary>모바일 목록에서 왼쪽으로 밀면 작업을 삭제한다.</summary>
    [JSInvokable]
    public async Task<bool> DeleteSwipedAsync(long taskKey)
    {
        var item = _rows.FirstOrDefault(t => t.TaskKey == taskKey);
        if (item is null)
        {
            return false;
        }

        if (!CanDeleteTask(item))
        {
            return true;
        }

        _rows = [.. _rows.Where(t => t.TaskKey != taskKey)];
        if (_picked?.TaskKey == taskKey)
        {
            _picked = null;
            _edit = null;
            _base = null;
        }
        if (_reveal?.TaskKey == taskKey)
        {
            _reveal = null;
        }
        await DropDraftAsync(taskKey);
        await InvokeAsync(StateHasChanged);

        try
        {
            await Api.DeleteAsync(taskKey);
            Say("지웠습니다.");
            return false;
        }
        catch (ApiException ex)
        {
            Say($"지우지 못했습니다 — {ex.Message}", NoticeTone.Error);
            await SearchAsync();
            return false;
        }
    }

    /// <summary>고르는 칸 한 줄. 공통코드에 없는 값들이라 화면이 들고 있다.</summary>
    public sealed record PickOption(string Value, string Text);

    private static readonly PickOption[] StatusFilters =
    [
        new("", "전체"),
        new("idle", "작성중"), new("queued", "대기"), new("preparing", "준비중"),
        new("running", "실행중"), new("succeeded", "완료"), new("failed", "실패"),
        new("timeout", "시간초과"), new("canceled", "취소"), new("interrupted", "중단"),
    ];

    /// <summary>칸별 필터 행의 목록. '전체'를 뺀 실제 상태만 담는다.</summary>
    private static readonly IReadOnlyList<PickOption> StatusFilterOptions =
        [.. StatusFilters.Where(x => !string.IsNullOrEmpty(x.Value))];

    private static readonly PickOption[] FlagFilters =
    [
        new("", "전체"),
        new("none", "안 함"), new("requested", "요청"), new("cancel_requested", "취소요청"),
    ];

    /// <summary>누가 올린 건인가(<c>ai_task.is_user_request</c>). 글자로 두는 것은 DxComboBox 의 「전체」 자리를 위해서다 — bool? 로 두면 「전체」가 null 이 되어 지우기와 구별되지 않는다.</summary>
    private static readonly PickOption[] SourceFilters =
    [
        new("", "전체"),
        new("user", "사용자 요청"), new("admin", "직접 작성"),
    ];

    private IReadOnlyList<AiModelOption> RunnerKinds = [];

    private static readonly PickOption[] NotifyWhens =
    [
        new("always", "끝나면 언제나"), new("on_success", "성공했을 때만"),
        new("on_failure", "실패했을 때만"),
    ];

    private IReadOnlyList<AiTaskDto> _rows = [];
    private IReadOnlyList<AiTargetDto> _targets = [];
    private IReadOnlyList<AiTargetDto> _enabledTargets = [];

    private AiTaskDto? _picked;

    /// <summary>오른쪽에서 고치는 것. 표의 행을 직접 고치지 않는다 — 저장 없이 옮기면 반쯤 고친 값이 표에 남는다.</summary>
    private AiTaskDto? _edit;

    /// <summary>서버가 들고 있는 그대로. 「고친 것이 있나」는 이것과 견준다 — 왼쪽 표 행과 견주면 목록 밖 건을 고칠 때 기준이 사라진다.</summary>
    private AiTaskDto? _base;

    private ConfirmDialog? _confirm;

    /// <summary>고른 작업의 실행 이력. 최근 것이 앞이다.</summary>
    private IReadOnlyList<AiTaskRunDto> _runs = [];

    /// <summary>지금 로그를 펼쳐 둔 실행.</summary>
    private AiTaskRunDto? _openRun;

    // ── 보는 탭 셋에 넘겨 줄 것 ──────────────────────────────
    // 받아 오는 일은 이 화면이 한다 — 탭이 따로 물으면 같은 값을 두 번 받아 어긋난다.

    /// <summary>차수별로 가른 지시와 결과. 오래된 차수가 앞이다. 고치는 중인 글이 아니라 서버가 들고 있는 AskItem 으로 짓는다.</summary>
    private IReadOnlyList<AiTaskTurn> _turns = [];

    /// <summary>펴 둔 차수가 만들고 고친 파일. 차수 띠가 있어 _openRun 하나만 넘긴다(AiTaskView 는 모든 차수를 쌓아 넘긴다).</summary>
    private IReadOnlyList<AiRunFileTurn> _changed = [];

    /// <summary>이 지시와 함께 올라온 파일. 목록은 개수만 싣으므로 이름은 건을 열 때 따로 묻는다. 「처리 파일」과 반대로 사람이 붙여 올린 것이다.</summary>
    private IReadOnlyList<AiTaskFileDto> _files = [];

    /// <summary>로그인한 사람의 아이디. <b>「내가 올린 건인가」만 본다.</b></summary>
    private string? _me;

    /// <summary>보는 탭 셋에 깔 건. 서버가 들고 있는 그대로이며 _edit 는 넘기지 않는다 — 고치는 사본을 깔면 보낸 적 없는 글이 「보낸 지시」에 앉는다. 새 작업은 서버에 없으니 _edit 로 떨어진다.</summary>
    private AiTaskDto? AskItem => _base ?? _edit;

    /// <summary>로그인한 사람이 올린 건인가. 아이디를 못 읽으면 거짓이고, 남긴 말은 관리자 경로로 돈다(AiTaskNotes).</summary>
    private bool IsMine =>
        _me is { Length: > 0 } me
        && string.Equals(me, AskItem?.CreId?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>펴 둔 차수가 고친 파일을 다시 읽는다. _openRun 이 바뀔 때마다 부르며, 그 차수 묶음 하나만 싼다.</summary>
    private void TakeRunFiles()
        => _changed = _openRun is { } run
            ? [new AiRunFileTurn(run.Seq, run, AiRunFiles.Parse(run.ChangedFiles, run.DiffStat))]
            : [];

    /// <summary>받아 둔 로그 줄. <b>그릴 때는 꼬리 500줄만</b> 쓴다.</summary>
    private readonly List<AiLogLineDto> _lines = [];

    /// <summary>마지막으로 받은 줄 번호. 다음에는 이것보다 큰 것만 달라고 한다.</summary>
    private int _lastSeq;

    /// <summary>돌고 있는 동안만 도는 타이머. 화면을 떠날 때 반드시 끈다 — 안 끄면 회로마다 쌓인다.</summary>
    private CancellationTokenSource? _tail;

    /// <summary>저장·요청이 오가는 중. 단추를 두 번 누르지 못하게 한다.</summary>
    private bool Busy;

    // ── 오른쪽 탭 넷 ──────────────────────────────────────────
    // 번호는 여기 것이다(부품은 제가 몇 번째인지 모른다). 상수로 둔다 — 맨숫자면 탭 하나 추가 때 한 자리라도 빠져 엉뚱한 탭이 열린다.

    /// <summary>적는 탭. 화면을 열면 여기가 선다.</summary>
    private const int TabWrite = 0;

    /// <summary>차수별 지시와 결과.</summary>
    private const int TabAsk = 1;

    /// <summary>이번 작업이 만들고 고친 파일.</summary>
    private const int TabFiles = 2;

    /// <summary>실행기가 찍은 줄.</summary>
    private const int TabLog = 3;

    /// <summary>지금 펴 둔 탭.</summary>
    private int _tab;

    /// <summary>사람이 탭을 직접 만졌다. 이후로는 화면이 저 혼자 탭을 넘기지 않는다 — 지시문을 고치는 중에 넘어가면 쓰던 글이 사라진다. 다른 건을 고르면 다시 켠다.</summary>
    private bool _tabPinned;

    private void OnTabClick(TabClickEventArgs _) => _tabPinned = true;

    private int RunningCount => _rows.Count(t => t.IsBusy || t.TaskStatus is "running" or "preparing");

    private int SucceededCount => _rows.Count(t => t.TaskStatus == "succeeded");

    private int AttentionCount => _rows.Count(t => t.TaskStatus is "failed" or "timeout" or "interrupted");

    /// <summary>돌기 시작했으면 로그 탭으로 넘긴다. 사람이 만진 뒤에는 넘기지 않는다.</summary>
    private void FollowRun()
    {
        if (!_tabPinned && _edit?.IsBusy == true)
        {
            _tab = TabLog;
        }
    }

    private string ListHint => $"{_rows.Count}건";

    private string EditTitle => _edit is null ? "내용"
        : _edit.TaskKey > 0 ? (string.IsNullOrWhiteSpace(_edit.Title) ? $"새 작업 · #{_edit.TaskKey}" : $"[요청 #{_edit.TaskKey}] {_edit.Title}") : (string.IsNullOrWhiteSpace(_edit.Title) ? "새 작업" : _edit.Title);

    private string EditHint => _edit is null ? string.Empty
        : _edit.TaskKey > 0 ? $"{_edit.StatusText}{(_edit.DurationText.Length > 0 ? $" · {_edit.DurationText}" : string.Empty)}"
        : "아직 저장하지 않았습니다";

    /// <summary>1단계에서는 집어 가는 쪽이 없음을 화면이 말한다. 「대기」는 안 집힌 것과 실패해 다시 넣은 것을 가른다(상태값은 queued 로 같다).</summary>
    private string BusyNotice => _edit switch
    {
        { TaskStatus: "queued", IsUnclaimed: true } =>
            "제한 시간이 지나도록 실행기가 집어 가지 않았습니다. 실행기가 떠 있는지 확인하십시오.",
        { TaskStatus: "queued", IsRetrying: true } =>
            "지난 실행이 실패해 다시 넣었습니다. 실행기가 집어 가기를 기다립니다.",
        { TaskStatus: "queued" } =>
            "요청했습니다. 실행기가 집어 가기를 기다립니다. 한참 이대로면 실행기가 떠 있는지 확인하십시오.",
        { TaskStatus: "preparing" } => "실행기가 집어 가 준비하고 있습니다.",
        _ => "지금 돌고 있습니다. 끝나거나 취소해야 고칠 수 있습니다.",
    };

    private bool CanEdit => _edit is not null && !_edit.IsBusy;

    private bool CanRequest => _edit is { TaskKey: > 0, IsBusy: false, TargetKey: not null };

    private bool CanCancel => _edit is { TaskKey: > 0 } e
        && (e.IsBusy || e.RequestFlag == "requested");

    /// <summary>고를 수 있는 AI. 대상의 RunnerKinds 가 허용한 것만 남긴다. 이 화면이 모르는 이름이면 그대로 한 줄을 세운다(조용히 빼면 못 고르는 이유를 알 수 없다).</summary>
    private IReadOnlyList<AiModelOption> AllowedRunners
    {
        get
        {
            if (_edit?.TargetKey is not { } key)
            {
                return [];
            }

            var target = _enabledTargets.FirstOrDefault(t => t.TargetKey == key);

            var allowed = (target?.RunnerKinds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (allowed.Length == 0)
            {
                return RunnerKinds;
            }

            return [.. allowed.Select(v =>
                RunnerKinds.FirstOrDefault(o => o.Value == v) ?? new AiModelOption(v, v))];
        }
    }

    // ── AI 고르개의 한도 ──────────────────────────────────────
    // 고르는 순간에 한도가 보여야 한다 — 다 쓴 CLI 는 집어 간 뒤에야 실패로 드러난다.
    //
    // 글자로 바꾸는 규칙은 AiUsageText 한 곳에만 둔다(화면마다 두면 같은 CLI 가 다른 이름으로 보인다).

    /// <summary>실행기가 올린 AI 별 한도. 못 읽으면 빈 목록이고, 고르개는 이름만 보여 준다.</summary>
    private IReadOnlyList<AiUsageSnapshot> _usage = [];

    /// <summary>한도를 한 번이라도 받아 봤나. 받기 전과 받았는데 빈 것은 다르다 — 가르지 않으면 아직 묻는 중인 CLI 가 「보고 없음」으로 보인다.</summary>
    private bool _usageSeen;

    /// <summary>그 CLI 의 한도 줄들. 한 CLI 가 여럿을 가질 수 있다.</summary>
    private List<AiUsageSnapshot> UsageOf(string? kind)
        => string.IsNullOrWhiteSpace(kind)
            ? []
            : [.. _usage.Where(u => string.Equals(u.RunnerKind, kind, StringComparison.OrdinalIgnoreCase))];

    /// <summary>한도를 읽어 둔다. 화면을 막지 않으며, 따라가기에 태우지 않는다 — 실행기는 15분마다 보고하므로 매초 읽을 이유가 없다.</summary>
    private async Task LoadUsageAsync()
    {
        _usage = await Dashboard.UsageAsync();

        // 못 읽었어도 참이다 — 「확인 중」에 두면 보고 없음과 게이트웨이 없음이 둘 다 「곧 올 것」으로 보인다.
        _usageSeen = true;
    }

    /// <summary>고른 대상이 push 를 허용하는가. 새 작업이면 목록에서 찾는다.</summary>
    private bool SelectedTargetAllowsPush =>
        _edit?.TargetKey is { } key
        && _enabledTargets.FirstOrDefault(t => t.TargetKey == key)?.AllowPush == true;

    private string PushLabel => SelectedTargetAllowsPush
        ? "저장소에 올리기 (운영 배포)"
        : "저장소에 올리기 (대상 미지원)";

    /// <summary>임시본을 사람마다 갈라 두려고 본다. 로그인 아이디만 쓴다.</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // 임시본은 사람마다 갈라 둔다 — 작업 번호만 열쇠로 쓰면 공용 PC 에서 남의 임시본이 뜬다.
        if (AuthState is not null)
        {
            var state = await AuthState;

            // 로그인 아이디이자 「내가 올린 건인가」의 열쇠다 — 거짓이면 남긴 말이 관리자 경로로 돈다.
            _me = state.User.Identity?.Name;
            Drafts.Use(_me);
        }

        await LoadModelsAsync();
        await LoadTargetsAsync();
        await SearchAsync();
    }

    private async Task LoadModelsAsync()
    {
        var models = await ModelCodes.GetAsync();
        RunnerKinds = models;
    }

    /// <summary>주소로 받은 작업 번호(<c>?task=</c>). 없으면 맨 앞 건이 열린다.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "task")]
    public long? TaskKey { get; set; }

    /// <summary>?key= 로 받는 작업 번호. PWA 알림 같은 외부 링크용이며 TaskKey 가 없을 때만 쓴다.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "key")]
    public long? KeyParam { get; set; }

    /// <summary>실제로 열어야 할 작업번호. <c>task</c> 우선, 없으면 <c>key</c>.</summary>
    private long? ResolvedTaskKey => TaskKey ?? KeyParam;

    /// <summary>주소로 받아 이미 골라 준 번호. 같은 번호로 두 번 고르지 않는다 — 다시 그릴 때마다 고르면 옮겨 간 줄이 되돌아간다.</summary>
    private long? _linked;

    /// <summary>주소가 지목한 건을 골라 준다. OnInitializedAsync 가 아니라 여기서 한다 — 탭이 떠 있으면 파라미터만 바뀌어 두 번째 건너오기도 먹는다.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (ResolvedTaskKey is not { } key || _linked == key)
        {
            return;
        }

        _linked = key;

        var row = _rows.FirstOrDefault(r => r.TaskKey == key);

        if (row is null)
        {
            // 조회 조건 때문에 목록에 없을 수 있다. 조건을 지우고 한 번 더 본다 — 없다고 보이면 링크가 헛것이 된다.
            _status = null;
            _flag = null;
            _targetKey = null;
            _keyword = null;
            _source = null;

            // 「완료 제외」도 끈다 — 끝난 건이 이 스위치 때문에 「찾지 못했습니다」가 되면 안 된다.
            _excludeDone = false;
            _excludeConfirmed = false;

            // 그리드 칸별 필터도 함께 지운다.
            _statusFilterValues = [];
            _grid?.Grid?.ClearFilter();

            await SearchAsync();

            row = _rows.FirstOrDefault(r => r.TaskKey == key);
        }

        if (row is null)
        {
            // 그 사이 지워졌다. 목록은 두고 왜 안 열렸는지만 알린다.
            Say($"#{key} 작업을 찾지 못했습니다. 지워졌을 수 있습니다.", NoticeTone.Warning);
            return;
        }

        await PickAsync(row);

        // 그 줄이 둘째 쪽에 있을 수 있다. 고르기만 하면 왼쪽 표에 켜진 줄이 안 보인다.
        _reveal = row;
    }

    /// <summary>다음 렌더에 표에서 보여 줄 줄. 쪽이 다르면 그 쪽으로 넘긴다.</summary>
    private AiTaskDto? _reveal;

    /// <summary>감싼 표. 쪽을 넘기려고 DevExpress API 를 직접 부른다.</summary>
    private CommGrd<AiTaskDto>? _grid;

    /// <summary>고른 줄이 지금 쪽에 없으면 그 줄이 있는 쪽으로 넘긴다. 자리 번호는 쪽 안이 아니라 전체 기준이고 정렬과도 달라서, _rows 에서 세지 않고 표에게 묻는다.</summary>
    private void RevealPicked()
    {
        var row = _reveal;
        _reveal = null;

        if (row is null || _grid?.Grid is not { } grid)
        {
            return;
        }

        for (var i = 0; i < grid.GetVisibleRowCount(); i++)
        {
            if (ReferenceEquals(grid.GetDataItem(i), row))
            {
                grid.MakeRowVisible(i);
                return;
            }
        }
    }

    /// <summary>대상 목록. 조회 실패로 화면을 막지 않는다 — 대상과 무관하게 작업 목록은 보여야 한다.</summary>
    private async Task LoadTargetsAsync()
    {
        try
        {
            _targets = await TargetApi.ListAsync();
            _enabledTargets = [.. _targets.Where(t => t.IsEnabled)];
        }
        catch (ApiException)
        {
            _targets = [];
            _enabledTargets = [];
        }
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _mobilePageCount = 1;
        _rows = await Api.ListAsync(_status, _flag, _targetKey, _keyword,
            userConfirmed: _excludeConfirmed ? false : null,
            userRequest: UserRequestFilter, excludeDone: _excludeDone);

        // 고른 것이 목록에서 빠졌으면 오른쪽을 비운다 — 없는 건을 있다고 믿게 두지 않는다.
        if (_picked is not null && _rows.All(r => r.TaskKey != _picked.TaskKey))
        {
            _picked = null;

            // 적어 둔 것이 있으면 편집 자리는 그대로 둔다 — 목록에서 빠졌을 뿐인데 쓰던 글을 치우면 사고다.
            if (_draftAt is null)
            {
                _edit = null;
                _base = null;
            }
        }

        return _rows.Count;
    }, "등록된 작업이 없습니다.", "작업을 읽지 못했습니다");

    private string? _status;
    private string? _flag;
    private long? _targetKey;
    private string? _keyword;
    private string? _source;

    /// <summary>끝난 건(succeeded · canceled)을 목록에서 뺀다. 「완료만 뺀다」는 상태 고르개로 못 해서 따로 둔다. 실패·시간초과·중단은 남긴다(사람이 할 일이 남았다). 거르는 일은 서버가 한다 — 화면에서 거르면 쪽마다 줄 수가 들쭉날쭉해진다.</summary>
    private bool _excludeDone;
    private bool _excludeConfirmed;

    /// <summary>고른 출처를 서버가 알아듣는 값으로. 「전체」는 조건 없음이다.</summary>
    private bool? UserRequestFilter => _source switch
    {
        "user" => true,
        "admin" => false,
        _ => null,
    };

    /// <summary>그리드 칸별 필터 행 — 상태 다중 선택. 빈 목록은 「전체」(필터 없음)다.</summary>
    private IEnumerable<string> _statusFilterValues = [];

    /// <summary>상태 다중 선택이 바뀌면 InOperator 로 묶어 FilterCriteria 에 반영한다. 선택이 없으면 null 로 지운다.</summary>
    private void OnStatusFilterChanged(
        IEnumerable<string> vals,
        GridDataColumnFilterRowCellTemplateContext filterCtx)
    {
        _statusFilterValues = vals;

        var list = vals.ToList();

        filterCtx.FilterCriteria = list.Count switch
        {
            0 => null,
            // 하나면 BinaryOperator 로 같음 비교한다.
            1 => new DevExpress.Data.Filtering.BinaryOperator(
                nameof(AiTaskDto.TaskStatus),
                list[0],
                DevExpress.Data.Filtering.BinaryOperatorType.Equal),
            // 둘 이상 — InOperator 로 묶는다.
            _ => new DevExpress.Data.Filtering.InOperator(
                nameof(AiTaskDto.TaskStatus),
                list.Cast<object>().ToArray()),
        };
    }

    private async Task PickAsync(AiTaskDto? item)
    {
        StopTail();

        _picked = item;
        _edit = item is null ? null : Copy(item);
        _base = item is null ? null : Copy(item);
        _runs = [];
        _openRun = null;
        _turns = [];
        _changed = [];
        _files = [];
        _lines.Clear();
        _lastSeq = 0;

        // 새 건을 골랐으면 새 맥락이다. 고정을 풀고 탭도 「작성」으로 되돌린다.
        _tabPinned = false;
        _tab = TabWrite;

        if (item is null)
        {
            _draftAt = null;
            return;
        }

        // 고른 건에 적어 둔 것이 있으면 그것을 얹는다.
        ApplyDraft(item);

        // 함께 올라온 파일은 못 읽어도 화면을 막지 않는다 — 첨부는 곁들이는 것이다.
        await LoadFilesAsync(item);

        await LoadRunsAsync();

        // 돌고 있는 건을 골랐으면 **보고 싶은 것은 로그다.**
        FollowRun();

        // 돌고 있거나 방금 끝난 건은 바로 펼친다 — 한 번 더 누르게 하면 아무 일도 안 난 것처럼 보인다.
        if (_runs.FirstOrDefault() is { } latest)
        {
            await OpenRunAsync(latest);
        }
        else if (item.IsBusy || item.TitlePending)
        {
            // 아직 집혀 가지 않았거나 제목 대기 중이다. 실행이 생기는 것을 기다린다.
            StartTail();
        }
    }

    /// <summary>실행 이력을 읽는다. 없어도 화면을 막지 않는다.</summary>
    private async Task LoadRunsAsync()
    {
        if (_edit is not { TaskKey: > 0 } e)
        {
            return;
        }

        try
        {
            _runs = await Api.RunsAsync(e.TaskKey);
        }
        catch (ApiException)
        {
            _runs = [];
        }

        TakeTurns();
    }

    /// <summary>실행 이력을 차수별로 가른다 — 실행 줄 하나가 한 차수다. 실행 이력이 바뀔 때만 다시 짓는다(따라가기가 매초 그릴 때 긴 지시문을 다시 가르지 않게).</summary>
    private void TakeTurns()
        => _turns = AskItem is { } task ? AiTaskTurns.Build(task, _runs) : [];

    /// <summary>함께 올라온 파일을 읽는다. 개수가 0 이면 묻지 않는다.</summary>
    private async Task LoadFilesAsync(AiTaskDto t)
    {
        if (t.FileCount <= 0)
        {
            _files = [];
            return;
        }

        try
        {
            _files = await Api.FilesAsync(t.TaskKey);
        }
        catch (ApiException)
        {
            _files = [];
        }
    }

    /// <summary>그 실행의 로그를 펼친다. 돌고 있으면 따라가기를 시작한다.</summary>
    private async Task OpenRunAsync(AiTaskRunDto run)
    {
        StopTail();

        _openRun = run;
        _lines.Clear();
        _lastSeq = 0;

        await FetchLogsAsync();

        if (run.IsRunning || _edit?.TitlePending == true)
        {
            StartTail();
        }

        // 고른 차수의 「처리 파일」을 그 실행 것으로 바꾼다 — 띠에서 1차를 눌렀는데 3차 파일이 서 있으면 먹통으로 보인다.
        TakeRunFiles();

        // 적는 탭을 보던 중이면 답이 있을 때 「지시와 결과」로, 없으면 로그로 건너간다.
        if (_tab == TabWrite)
        {
            _tab = !string.IsNullOrWhiteSpace(run.ResultText) ? TabAsk : TabLog;
        }
    }

    /// <summary>새 줄만 받아 이어 붙인다.</summary>
    private async Task<bool> FetchLogsAsync()
    {
        if (_openRun is not { } run)
        {
            return false;
        }

        try
        {
            var more = await Api.LogsAsync(run.RunKey, _lastSeq);

            if (more.Count == 0)
            {
                return false;
            }

            _lines.AddRange(more);
            _lastSeq = more[^1].Seq;

            // 들고 있는 줄에도 상한을 둔다 — 수만 줄을 회로로 실어 나르지 않게.
            if (_lines.Count > 2000)
            {
                _lines.RemoveRange(0, _lines.Count - 2000);
            }

            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    /// <summary>작업이 도는 동안 따라간다. 실행이 아직 없을 때도 돈다 — 「요청」 직후에는 집어 가기 전이라 실행 줄이 없다. 매초 보는 것은 작업 상태와 펴 둔 실행의 로그 꼬리다.</summary>
    private void StartTail()
    {
        StopTail();

        _tail = new CancellationTokenSource();
        var ct = _tail.Token;

        // 가동 시간도 같은 토큰을 쓰므로 StopTail() 하나로 같이 멈춘다.
        StartClock(ct);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);

                    var changed = await FetchLogsAsync();

                    var latest = _edit is { TaskKey: > 0 } e ? await SafeGetAsync(e.TaskKey) : null;

                    if (latest is not null && _edit is not null)
                    {
                        if (_edit.TaskStatus != latest.TaskStatus)
                        {
                            changed = true;
                        }

                        // 사람이 치고 있는 제목은 덮지 않는다 (_edit 가 _base 와 다르면 손대지 않음)
                        if (_edit.Title == _base?.Title && _edit.Title != latest.Title)
                        {
                            _edit.Title = latest.Title;
                            if (_base is not null)
                            {
                                _base.Title = latest.Title;
                            }
                            changed = true;
                        }
                        _edit.TitleAuto = latest.TitleAuto;
                        _edit.TitleRunKey = latest.TitleRunKey;

                        _edit.TaskStatus = latest.TaskStatus;
                        _edit.RequestFlag = latest.RequestFlag;
                        _edit.DurationMs = latest.DurationMs;

                        // 끝난 시각도 가져온다 — 「대기」가 새 요청인지 재시도인지는 IsRetrying 으로 가른다.
                        _edit.FinishedAt = latest.FinishedAt;
                        _edit.LastError = latest.LastError;
                        _edit.NotifyError = latest.NotifyError;

                        // 왼쪽 표의 행도 함께 맞춘다 — _edit 는 표 행과 일부러 갈라 둔 복사본이라 여기만 고치면 두 칸이 어긋난다.
                        SyncRow(latest);

                        // 다른 데서 요청해 돌기 시작했을 수도 있다. 그때도 넘긴다.
                        FollowRun();

                        // 실행이 새로 생겼거나 끝났으면 이력을 다시 읽는다.
                        var wantRuns = _openRun is null
                            || !latest.IsBusy
                            || _runs.Count == 0
                            || (latest.LastRunKey is { } k && k != _openRun.RunKey);

                        if (wantRuns)
                        {
                            await LoadRunsAsync();

                            if (_runs.FirstOrDefault() is { } newest
                                && newest.RunKey != _openRun?.RunKey)
                            {
                                _openRun = newest;
                                _lines.Clear();
                                _lastSeq = 0;
                                await FetchLogsAsync();
                            }
                            else if (_openRun is not null)
                            {
                                _openRun = _runs.FirstOrDefault(r => r.RunKey == _openRun.RunKey) ?? _openRun;
                            }

                            // 펴 둔 차수가 갈렸거나 그 줄이 새것이면 처리 파일도 맞춘다 — 안 맞추면 끝난 뒤에도 빈 목록이 선다.
                            TakeRunFiles();

                            changed = true;
                        }

                        // 다 끝났으면 따라가기를 멈춘다 — 안 멈추면 회로가 2초마다 서버를 두들긴다.
                        if (!latest.IsBusy && !latest.TitlePending && _openRun?.IsRunning != true)
                        {
                            await InvokeAsync(StateHasChanged);
                            StopTail();
                            return;
                        }
                    }

                    if (changed)
                    {
                        await InvokeAsync(StateHasChanged);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 화면을 떠났다. 조용히 끝낸다.
            }
        }, ct);
    }

    /// <summary>1초마다 화면만 다시 그린다. 서버에 묻지 않는다 — 가동 시간은 시작과 지금의 차이라서, 따라가기를 1초로 당기면 서버 부하가 두 배가 된다.</summary>
    private void StartClock(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException)
            {
                // 다 끝났거나 화면을 떠났다. 조용히 끝낸다.
            }
        }, ct);
    }

    private void StopTail()
    {
        _tail?.Cancel();
        _tail?.Dispose();
        _tail = null;
    }

    private async Task<AiTaskDto?> SafeGetAsync(long key)
    {
        try
        {
            return await Api.GetAsync(key);
        }
        catch (ApiException)
        {
            return null;
        }
    }

    // ── 로그 탭에 넘겨 줄 것 ──────────────────────────────────
    // 그리기와 따라 내리기는 AiTaskLogTab 이 한다. 여기서 밀어 줄 상자는 없다.

    /// <summary>탭에 그릴 줄 수 상한. 회로로 실어 보내는 양의 상한이라 들고 있는 2,000줄과는 다르다. 넘으면 꼬리만 넘긴다.</summary>
    private const int LogDrawMax = 500;

    /// <summary>탭에 그릴 줄 — 꼬리 LogDrawMax 줄만 넘긴다. 잘렸으면 맨 앞에 잘렸다는 한 줄을 넣는다(말없이 자르면 앞쪽 오류를 없는 것으로 읽는다).</summary>
    private IReadOnlyList<string> LogLines
    {
        get
        {
            if (_lines.Count <= LogDrawMax)
            {
                return [.. _lines.Select(l => l.Text ?? string.Empty)];
            }

            return
            [
                $"… 앞 {_lines.Count - LogDrawMax}줄은 줄였습니다",
                .. _lines.Skip(_lines.Count - LogDrawMax).Select(l => l.Text ?? string.Empty),
            ];
        }
    }

    /// <summary>로그 전체 줄 수(탭 이름에 적는다). 받아 둔 줄만 세면 상한 때문에 작으므로 실행 줄의 log_line_count 와 큰 쪽을 쓴다.</summary>
    private int LogCount => Math.Max(_lines.Count, _openRun?.LogLineCount ?? 0);

    /// <summary>옮긴 글을 담을 열쇠. 차수까지 넣는다 — 건 번호만이면 1차를 보다 3차로 옮길 때 1차 번역이 3차 줄 옆에 선다.</summary>
    private string LogStateKey => $"ai-task:{_edit?.TaskKey ?? 0}:{_openRun?.RunKey ?? 0}";

    /// <summary>「로그 보기」를 눌렀다. 고정도 함께 건다 — 따라가기가 다음 바퀴에 탭을 또 옮기면 방금 고른 것이 뒤집힌다.</summary>
    private void OpenLogTab()
    {
        _tabPinned = true;
        _tab = TabLog;
    }

    /// <summary>그 실행이 얼마나 돌았나. 시각은 UTC 라 AppTime.UtcNow 와 견주되, 두 시계가 어긋나 음수가 나지 않게 막는다.</summary>
    private static string RunElapsedText(AiTaskRunDto run)
    {
        if (run.StartedAt is not { } started)
        {
            return string.Empty;
        }

        // 도는 중이면 지금까지, 끝났으면 끝 시각까지. 끝시각 없이 끝난 건(취소·중단)은 셀 것이 없다.
        var ended = run.FinishedAt ?? (run.IsRunning ? AppTime.UtcNow : null);

        if (ended is not { } stop)
        {
            return string.Empty;
        }

        var span = stop - started;

        return ElapsedText(span < TimeSpan.Zero ? TimeSpan.Zero : span);
    }

    /// <summary>3:07 · 1:02:03 꼴. 한 시간을 넘겨야 시각 자리를 늘린다.</summary>
    private static string ElapsedText(TimeSpan span) =>
        span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";

    /// <summary>대상을 바꿨다. 못 쓰는 AI 가 남으면 콤보는 빈칸인데 옛 값이 저장되므로, 여기서 AI 와 「올리기」를 대상에 맞춘다.</summary>
    private void PickTarget(long? key)
    {
        if (_edit is null)
        {
            return;
        }

        _edit.TargetKey = key;
        _edit.TargetRunnerKinds = _enabledTargets.FirstOrDefault(t => t.TargetKey == key)?.RunnerKinds;
        FixRunnerForTarget();

        // 허용하지 않는 대상이면 「올리기」를 끈다 — 서버도 저장할 때 끄지만, 켜진 채로 보이면 사람이 그대로 나간다고 믿는다.
        if (!SelectedTargetAllowsPush)
        {
            _edit.AutoPush = false;
        }
    }

    private void FixRunnerForTarget()
    {
        if (_edit is null)
        {
            return;
        }

        var allowed = AllowedRunners;

        if (allowed.Count == 0)
        {
            return;
        }

        var matched = allowed.FirstOrDefault(o => string.Equals(o.Value, _edit.RunnerKind, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
        {
            _edit.RunnerKind = matched.Value;
        }
        else
        {
            _edit.RunnerKind = allowed[0].Value;
        }
    }

    /// <summary>새 작업. 대상이 하나뿐이면 미리 고른다. 메일 받기는 켠 채로 연다 — 몇십 분 걸리는 일이라 꺼 두면 끝난 것을 아무도 모른다. 받는 사람은 비운다(요청한 사람에게 간다).</summary>
    private void NewTask()
    {
        var fresh = new AiTaskDto();

        FillNewTask(fresh);
        OpenTask(fresh, isNew: true);
    }

    /// <summary>새 작업의 기본값. 표의 ＋ 도 이것을 거쳐 온다(<c>OnNew</c>).</summary>
    private void FillNewTask(AiTaskDto task)
    {
        task.RunnerKind = RunnerKinds.FirstOrDefault()?.Value ?? "claude";
        task.TimeoutMinutes = 120;
        task.AttemptMax = 3;
        task.NotifyEmail = true;
        task.NotifyWhen = "always";
        task.TargetKey = _targetKey ?? (_enabledTargets.Count == 1 ? _enabledTargets[0].TargetKey : null);
    }

    /// <summary>표가 넘긴 줄을 오른쪽 편집 자리에 앉힌다. 새로 만드는 것만 이리로 온다 — 있는 건은 줄을 누르는 것이 곧 편집이다(PickAsync).</summary>
    private void OpenTask(AiTaskDto task, bool isNew)
    {
        if (!isNew)
        {
            return;
        }

        _picked = null;
        _base = null;
        _draftAt = null;
        _runs = [];
        _openRun = null;
        _turns = [];
        _changed = [];
        _files = [];
        _tabPinned = false;
        _tab = TabWrite;

        _edit = task;
    }

    private async Task SaveAsync()
    {
        if (_edit is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_edit.Contents))
        {
            Say("내용을 적어 주십시오.", NoticeTone.Warning);
            return;
        }

        Busy = true;

        try
        {
            // 임시본의 열쇠다. 새 작업은 저장하면 번호가 붙으므로 저장 전 번호를 들고 있어야 그것을 버릴 수 있다.
            var draftKey = _edit.TaskKey;

            // 표가 있으면 표의 저장 길로 보내고(OnSave → PersistAsync), 표가 없는 모바일은 직접 저장한다.
            bool saved;
            if (_grid is not null)
            {
                saved = await _grid.SaveAsync(_edit, _edit.TaskKey == 0);
            }
            else
            {
                var isNew = _edit.TaskKey == 0;
                saved = await RunAsync(
                    () => PersistAsync((_edit, isNew)),
                    isNew ? "등록했습니다." : "저장했습니다.",
                    isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

                if (saved)
                {
                    await SearchAsync();
                }
            }

            if (saved)
            {
                // 서버에 들어갔다. 임시본은 할 일을 다 했다.
                await DropDraftAsync(draftKey);

                SyncPicked();
            }
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>실제로 서버에 넣는다. 표의 OnSave 와 「요청」 경로가 같이 쓴다 — 등록·수정을 가르는 줄이 두 곳에 있으면 갈린다.</summary>
    private async Task PersistAsync((AiTaskDto Item, bool IsNew) e)
    {
        var result = e.IsNew
            ? await Api.CreateAsync(e.Item)
            : await Api.UpdateAsync(e.Item);

        Take(result);
    }

    /// <summary>AI 에게 시킨다. 먼저 저장한다 — 화면의 글과 실행기가 집어 갈 글이 달라지는 것이 제일 나쁜 어긋남이다.</summary>
    private async Task RequestAsync()
    {
        if (_edit is null || _confirm is null)
        {
            return;
        }

        var target = _enabledTargets.FirstOrDefault(t => t.TargetKey == _edit.TargetKey);

        var warning = _edit.AutoPush
            ? "\n\n※ 끝나면 저장소에 올립니다 — 운영 배포가 일어납니다."
            : string.Empty;

        var ok = await _confirm.AskAsync(
            $"「{_edit.Title}」 을(를) {target?.TargetNm ?? "고른 대상"} 에서 {_edit.RunnerKind} 로 실행합니다.{warning}",
            title: "AI 에게 요청",
            confirmText: "요청",
            confirmStyle: ButtonRenderStyle.Primary);

        if (!ok)
        {
            return;
        }

        Busy = true;

        try
        {
            var draftKey = _edit.TaskKey;

            // 요청 전에 저장한다. 저장이 실패하면 요청하지 않는다.
            if (_edit.TaskKey == 0 || HasChanges())
            {
                var saved = await RunAsync(
                    () => PersistAsync((_edit, _edit.TaskKey == 0)),
                    "저장했습니다.", "저장하지 못해 요청하지 않았습니다");

                if (!saved)
                {
                    return;
                }

                await DropDraftAsync(draftKey);
            }

            var requested = await RunAsync(
                async () =>
                {
                    var result = await Api.RequestAsync(_edit!.TaskKey);

                    Take(result);
                },
                "요청했습니다. 실행기가 집어 가기를 기다립니다.",
                "요청하지 못했습니다");

            if (requested)
            {
                await SearchAsync();
                SyncPicked();

                // 여기서 따라가기를 건다. 안 걸면 화면이 「대기」에서 움직이지 않는다.
                StartTail();

                // 방금 요청한 사람이 보고 싶은 것은 로그라서, 이 한 번은 고정을 무시하고 넘긴다.
                _tabPinned = false;
                _tab = TabLog;
            }
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task CancelAsync()
    {
        if (_edit is null)
        {
            return;
        }

        Busy = true;

        try
        {
            var done = await RunAsync(
                async () =>
                {
                    var result = await Api.CancelAsync(_edit!.TaskKey);

                    Take(result);
                },
                "취소했습니다.", "취소하지 못했습니다");

            if (done)
            {
                await SearchAsync();
                SyncPicked();
            }
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task AskDeleteAsync()
    {
        if (_edit is null || _confirm is null)
        {
            return;
        }

        var ok = await _confirm.AskAsync(
            $"「{_edit.Title}」 을(를) 지웁니다.\n실행 이력은 남습니다.",
            title: "작업 삭제");

        if (!ok)
        {
            return;
        }

        var done = await RunAsync(
            () => Api.DeleteAsync(_edit.TaskKey),
            "지웠습니다.", "지우지 못했습니다");

        if (done)
        {
            // 작업이 없어졌다. 임시본을 남기면 없는 건을 이어 쓰려고 한다.
            await DropDraftAsync(_edit.TaskKey);

            _picked = null;
            _edit = null;
            _base = null;
            await SearchAsync();
        }
    }

    // ── AI 도우미가 돌려준 것 ──
    // 짓고 간추리는 일은 AiTaskWriteAssist 가 하고, 화면은 담을 자리만 정한다 — 편집 상태를 부품이 알지 않게 하려고.

    /// <summary>AI 가 지은 제목 중 사람이 고른 것. 임시저장은 다음 렌더의 TrackDraftAsync 가 견줘 저절로 건다(칸마다 변경 처리기를 달지 않는다).</summary>
    private void TakeAiTitle(string title)
    {
        if (_edit is null || !CanEdit)
        {
            return;
        }

        _edit.Title = title;

        // 저장하면 서버가 같은 값을 넣는다. 저장 전에도 화면이 알아야 「제목 기다리는 중」이 뜨지 않는다.
        _edit.TitleAuto = false;

        Say("제목을 바꿨습니다. 「저장」을 눌러야 서버에 들어갑니다.");
    }

    /// <summary>AI 가 간추린 조각을 본문 맨 위에 얹는다. 덮어쓰지 않는다 — 실행기가 집어 가는 것은 본문 그대로다.</summary>
    private void TakeAiSummary(string markdown)
    {
        if (_edit is null || !CanEdit)
        {
            return;
        }

        var body = _edit.Contents ?? string.Empty;

        _edit.Contents = markdown + body.TrimStart('\n');

        Say("지시 내용 맨 위에 요약을 얹었습니다. 「저장」을 눌러야 서버에 들어갑니다.");
    }

    private AiTaskContinuePopup? _continuePopup;

    /// <summary>이어서 시킬 수 있나. 끝난 건에만 — 도는 것에 얹으면 지금 도는 것과 다음 것이 섞인다.</summary>
    private bool CanContinue => _edit is { TaskKey: > 0, IsBusy: false }
        && _runs.Count > 0;

    private async Task AskContinueAsync()
    {
        if (_edit is { } t && _continuePopup is not null)
        {
            await _continuePopup.OpenAsync(t);
        }
    }

    private async Task OnContinueFinishedAsync(AiTaskDto updated)
    {
        Take(updated);
        await DropDraftAsync(updated.TaskKey);
        await SearchAsync();
        SyncPicked();
        StartTail();
        _tabPinned = false;
        _tab = TabLog;
    }

    /// <summary>저장한 뒤 왼쪽 표의 고른 행을 새 값으로 맞춘다.</summary>
    private void SyncPicked()
    {
        if (_edit is { TaskKey: > 0 })
        {
            _picked = _rows.FirstOrDefault(r => r.TaskKey == _edit.TaskKey);
        }
    }

    /// <summary>고친 것이 있나. 요청할 때 헛저장을 줄이고 임시저장할지를 가른다.</summary>
    private bool HasChanges()
    {
        if (_base is null || _edit is null)
        {
            return true;
        }

        return _base.Title != _edit.Title
            || _base.Contents != _edit.Contents
            || _base.TargetKey != _edit.TargetKey
            || _base.RunnerKind != _edit.RunnerKind
            || _base.TimeoutMinutes != _edit.TimeoutMinutes
            || _base.AutoPush != _edit.AutoPush
            || _base.NotifyEmail != _edit.NotifyEmail
            || _base.NotifyTo != _edit.NotifyTo
            || _base.NotifyWhen != _edit.NotifyWhen;
    }

    /// <summary>고친 것이 있으면 임시저장을 건다. 칸마다 변경 처리기를 달지 않고 렌더 끝에 지금 모양과 마지막 모양을 견준다 — 새 칸을 빠뜨릴 자리가 없다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // 적어 둔 것은 회로가 붙은 뒤에야 읽을 수 있다(JS 왕복).
            if (_linked is not null && _picked is not null)
            {
                await ResumeLinkedDraftAsync();
            }
            else
            {
                await ResumeDraftAsync();
            }

            // 목록 다음이다. 한도는 곁들이는 값이라 목록을 늦추지 않는다 — 그동안 고르개는 「한도 확인 중」으로 선다.
            await LoadUsageAsync();

            RevealPicked();
            if (_isPhone)
            {
                await AttachSwipeAsync();
            }
            StateHasChanged();
            return;
        }

        RevealPicked();

        await TrackDraftAsync();

        if (_isPhone)
        {
            await AttachSwipeAsync();
        }
    }

    /// <summary>화면을 떠난다. 타이머를 끄고 기다리던 임시본을 지금 적는다 — 회로가 살아 있는 동안에만 적힌다.</summary>
    public void Dispose()
    {
        StopTail();

        CancelDraftDelay();
        _ = WriteDraftAsync();

        if (_swipeRef is not null)
        {
            _swipeRef.Dispose();
            _swipeRef = null;
        }

        if (_swipeModule is { } swipeModule)
        {
            _swipeModule = null;
            _ = ReleaseAsync(swipeModule);
        }
    }

    private static async Task ReleaseAsync(IJSObjectReference js)
    {
        try
        {
            await js.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }

    // ── 임시저장 ──
    // 어떻게 적는지는 AiTaskDraftStore 에 있다. 여기는 언제 적고 언제 버리는가만 정한다.

    /// <summary>화면을 열 때 말없이 이어서 열어 주는 기한. 기한이 없으면 사흘 전 임시본이 열 때마다 열린다 — 지난 것은 목록의 「임시」 표로만 알린다.</summary>
    private static readonly TimeSpan ResumeWindow = TimeSpan.FromHours(12);

    /// <summary>입력이 이만큼 멈추면 적어 둔다. 편집기가 이미 300ms 를 기다리므로 합쳐 2초 안쪽이다(왕복이 브라우저까지 가서 타자마다 두들기지 않게).</summary>
    private const int DraftDelayMs = 1500;

    /// <summary>마지막으로 적어 둔 임시본의 모양. 같은 것을 두 번 적지 않도록 견주는 값이다.</summary>
    private string? _draftShape;

    /// <summary>고치는 것이 임시저장된 때. null 이면 임시본이 없다.</summary>
    private DateTime? _draftAt;

    /// <summary>적기를 기다리는 중. 새 입력이 오면 이전 대기를 취소한다.</summary>
    private CancellationTokenSource? _draftDelay;

    /// <summary>기다리는 중인 것. 화면을 떠날 때 이것을 바로 적는다.</summary>
    private AiTaskDraft? _pendingDraft;

    /// <summary>기다리는 것의 작업 번호. _pendingDraft 가 null 이면 버리라는 뜻이다.</summary>
    private long? _pendingKey;

    /// <summary>지금 고치는 것에서 임시본을 뜬다. <b>사람이 고치는 칸만</b> 담는다.</summary>
    private AiTaskDraft DraftOf(AiTaskDto e) => new()
    {
        TaskKey = e.TaskKey,
        Title = e.Title,
        Contents = e.Contents,
        TargetKey = e.TargetKey,
        RunnerKind = e.RunnerKind,
        TimeoutMinutes = e.TimeoutMinutes,
        AutoPush = e.AutoPush,
        NotifyEmail = e.NotifyEmail,
        NotifyTo = e.NotifyTo,
        NotifyWhen = e.NotifyWhen,
        BaseRowVersion = _base?.RowVersion ?? 0,
    };

    /// <summary>고친 것이 있으면 잠시 뒤에 적고, 없어졌으면 적어 둔 것을 버린다. 렌더가 끝날 때마다 불린다.</summary>
    private async Task TrackDraftAsync()
    {
        // 기다리던 것이 다른 건의 것이면 먼저 적는다 — 덮으면 1.5초 안에 다른 줄을 누른 사람의 마지막 문장이 사라진다.
        if (_pendingKey is { } waiting && waiting != (_edit?.TaskKey ?? -1))
        {
            CancelDraftDelay();
            await WriteDraftAsync();
        }

        if (_edit is null || _edit.IsBusy)
        {
            // 도는 중에는 고칠 수 없다. 적어 둘 것도 없다.
            return;
        }

        // 새 작업은 무엇이라도 적혀 있을 때만 담는다 — 빈 새 작업을 담으면 다음에 화면을 열 때마다 빈 새 작업이 열린다.
        var worth = _edit.TaskKey > 0
            || !string.IsNullOrWhiteSpace(_edit.Contents)
            || !string.IsNullOrWhiteSpace(_edit.Title);

        var draft = HasChanges() && worth ? DraftOf(_edit) : null;

        // 적을 때 시각을 찍으므로(SaveAsync) 이 모양은 내용이 같으면 같다 — 견줄 값으로 쓸 수 있다.
        var shape = draft is null
            ? $"none:{_edit.TaskKey}"
            : JsonSerializer.Serialize(draft);

        if (shape == _draftShape)
        {
            return;
        }

        _draftShape = shape;

        CancelDraftDelay();

        _pendingKey = _edit.TaskKey;
        _pendingDraft = draft;

        _draftDelay = new CancellationTokenSource();
        var ct = _draftDelay.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DraftDelayMs, ct);

                // 회로의 차례를 빌려 적는다 — JS 왕복은 렌더 순서 안에서 일어나야 한다.
                await InvokeAsync(async () =>
                {
                    await WriteDraftAsync();
                    StateHasChanged();
                });
            }
            catch (OperationCanceledException)
            {
                // 더 새 입력이 왔다. 그쪽 대기가 적는다.
            }
        }, ct);
    }

    /// <summary>기다리던 것을 지금 적는다(또는 버린다). 기다리는 것이 없으면 아무 일도 안 한다.</summary>
    private async Task WriteDraftAsync()
    {
        if (_pendingKey is not { } key)
        {
            return;
        }

        var draft = _pendingDraft;

        _pendingKey = null;
        _pendingDraft = null;

        if (draft is null)
        {
            await Drafts.RemoveAsync(key);

            if (_edit?.TaskKey == key)
            {
                _draftAt = null;
            }

            return;
        }

        await Drafts.SaveAsync(draft);

        // 적은 뒤 딴 건으로 옮겼을 수 있다. 그때 이 시각을 걸면 엉뚱한 건이 임시저장된 것처럼 보인다.
        if (_edit?.TaskKey == key)
        {
            _draftAt = draft.SavedAt;
        }
    }

    private void CancelDraftDelay()
    {
        _draftDelay?.Cancel();
        _draftDelay?.Dispose();
        _draftDelay = null;
    }

    /// <summary>임시본을 버린다. 서버에 들어갔거나 사라진 건이다.</summary>
    private async Task DropDraftAsync(long taskKey)
    {
        CancelDraftDelay();

        _pendingKey = null;
        _pendingDraft = null;
        _draftShape = null;
        _draftAt = null;

        await Drafts.RemoveAsync(taskKey);
    }

    /// <summary>그 건에 적어 둔 것이 있으면 편집 자리에 얹는다. 도는 중인 건에는 얹지 않는다 — 실행기가 들고 간 글과 다시 어긋난다.</summary>
    private void ApplyDraft(AiTaskDto server)
    {
        _draftAt = null;

        if (_edit is null || server.IsBusy || !Drafts.Items.TryGetValue(server.TaskKey, out var draft))
        {
            return;
        }

        Overlay(draft);

        // 그 사이 누군가 저장했다. 그대로 저장하면 그 사람의 글이 덮이므로 말해 주는 데서 멈춘다(자동 합치기 금지).
        if (server.TaskKey > 0 && draft.BaseRowVersion != server.RowVersion)
        {
            Say("임시본을 뜬 뒤에 서버 내용이 바뀌었습니다. 저장하면 그쪽 내용이 덮입니다.",
                NoticeTone.Warning);
        }
    }

    /// <summary>적어 둔 값을 편집 자리에 옮겨 담는다.</summary>
    private void Overlay(AiTaskDraft draft)
    {
        if (_edit is null)
        {
            return;
        }

        _edit.Title = draft.Title;
        _edit.Contents = draft.Contents;
        _edit.TargetKey = draft.TargetKey;
        _edit.RunnerKind = draft.RunnerKind;
        _edit.TimeoutMinutes = draft.TimeoutMinutes;
        _edit.AutoPush = draft.AutoPush;
        _edit.NotifyEmail = draft.NotifyEmail;
        _edit.NotifyTo = draft.NotifyTo;
        _edit.NotifyWhen = draft.NotifyWhen;

        _draftAt = draft.SavedAt;

        // 방금 얹은 것을 곧바로 다시 적지 않게 모양을 맞춰 둔다.
        _draftShape = JsonSerializer.Serialize(DraftOf(_edit));
    }

    /// <summary>화면이 다시 뜰 때 적어 둔 것이 있으면 가장 최근 것을 이어서 연다(탭을 옮겼다 돌아온 사람이 보는 길).</summary>
    private async Task ResumeDraftAsync()
    {
        var drafts = await Drafts.ReadAsync();

        if (drafts.Count == 0)
        {
            return;
        }

        var latest = drafts.Values.OrderByDescending(d => d.SavedAt).First();

        // 오래된 것은 열지 않고 있다는 것만 알린다 — 목록의 「임시」 표를 누르면 그때 얹힌다.
        if (AppTime.UtcNow - latest.SavedAt > ResumeWindow)
        {
            Say($"적어 둔 임시본이 {drafts.Count}건 있습니다. 목록 머리줄의 「임시저장」 단추에서 불러올 수 있습니다.");
            return;
        }

        if (latest.TaskKey == 0)
        {
            await OpenDraftAsync(latest);
            Say($"적어 두었던 새 작업을 이어서 씁니다 ({latest.SavedAt.Kst("MM-dd HH:mm")}).");
            return;
        }

        // 작업이 지워졌으면 임시본도 함께 버려졌다. 화면이 열리는 길이라 말은 하지 않는다.
        if (!await OpenDraftAsync(latest))
        {
            return;
        }

        // 도는 중이었으면 얹지 않았다(`ApplyDraft`). 그때는 말도 하지 않는다.
        if (_draftAt is not null)
        {
            Say($"「{DraftTitle(latest)}」 에 적어 두었던 내용을 이어서 씁니다 ({latest.SavedAt.Kst("MM-dd HH:mm")}).");
        }
    }

    // ── 적어 둔 것을 불러오는 자리 ──
    // 저장 안 한 새 작업, 열두 시간 지난 임시본, 조회 조건 밖의 건은 목록과 최근 열기로 닿지 않아 적어 둔 것 전부를 늘어놓는 창을 둔다.

    /// <summary>적어 둔 것 목록 창이 열려 있나.</summary>
    private bool _draftsOpen;

    /// <summary>적어 둔 것 전부. <b>최근 것이 앞</b>이다.</summary>
    private IReadOnlyList<AiTaskDraft> DraftList =>
        [.. Drafts.Items.Values.OrderByDescending(d => d.SavedAt)];

    /// <summary>단추에 적는 건수.</summary>
    private int DraftCount => Drafts.Items.Count;

    /// <summary>적어 둔 것 목록을 연다. 화면이 뜰 때 이미 읽었어도 한 번 더 읽는다 — 주소로 건너온 길은 읽기를 건너뛸 수 있다.</summary>
    private async Task OpenDraftsAsync()
    {
        await Drafts.ReadAsync();
        _draftsOpen = true;
    }

    /// <summary>적어 둔 한 벌을 편집 자리에 연다. 그 작업이 없어졌으면 거짓이고 임시본도 함께 버려진다.</summary>
    private async Task<bool> OpenDraftAsync(AiTaskDraft draft)
    {
        if (draft.TaskKey == 0)
        {
            NewTask();
            Overlay(draft);
            return true;
        }

        // 조건 때문에 목록에 없을 수 있다. 그때는 한 건만 따로 읽는다 — 쓰던 글이 조회 조건 때문에 안 열리면 안 된다.
        var row = _rows.FirstOrDefault(r => r.TaskKey == draft.TaskKey)
            ?? await SafeGetAsync(draft.TaskKey);

        if (row is null)
        {
            await Drafts.RemoveAsync(draft.TaskKey);
            return false;
        }

        await PickAsync(row);
        return true;
    }

    /// <summary>창에서 고른 임시본을 연다.</summary>
    private async Task PickDraftAsync(AiTaskDraft draft)
    {
        _draftsOpen = false;

        if (!await OpenDraftAsync(draft))
        {
            Say($"#{draft.TaskKey} 작업이 없어졌습니다. 적어 두었던 내용도 함께 버립니다.",
                NoticeTone.Warning);
            return;
        }

        // 도는 중인 건에는 얹지 않는다(ApplyDraft). 그때는 그렇게 말해야 「안 불러와졌다」로 읽히지 않는다.
        if (draft.TaskKey > 0 && _draftAt is null)
        {
            Say("지금 돌고 있는 작업이라 적어 둔 내용을 얹지 않았습니다. 끝나거나 취소한 뒤에 다시 불러오십시오.",
                NoticeTone.Warning);
            return;
        }

        Say($"「{DraftTitle(draft)}」 에 적어 두었던 내용을 이어서 씁니다 ({draft.SavedAt.Kst("MM-dd HH:mm")}).");
    }

    /// <summary>창에서 임시본 하나를 버린다. 지금 고치는 것이면 화면도 되돌린다 — 저장소만 지우면 다음 렌더의 TrackDraftAsync 가 그 글을 다시 적어 버린다.</summary>
    private async Task DropDraftRowAsync(AiTaskDraft draft)
    {
        if (_confirm is null)
        {
            return;
        }

        var ok = await _confirm.AskAsync(
            $"「{DraftTitle(draft)}」 에 적어 둔 내용을 버립니다.",
            title: "임시본 버리기");

        if (!ok)
        {
            return;
        }

        if (_edit?.TaskKey == draft.TaskKey)
        {
            await DropDraftAsync(draft.TaskKey);

            if (_base is not null)
            {
                _edit = Copy(_base);
            }
            else
            {
                _edit = null;
                _picked = null;
            }
        }
        else
        {
            await Drafts.RemoveAsync(draft.TaskKey);
        }

        // 마지막 하나를 버렸으면 창에 보여 줄 것이 없다.
        if (Drafts.Items.Count == 0)
        {
            _draftsOpen = false;
        }
    }

    /// <summary>임시본 한 벌을 한 줄로 부르는 이름. 제목이 비었으면 본문 첫 줄을 쓴다(목록에 「제목 없음」이 줄줄이 서지 않게).</summary>
    private static string DraftTitle(AiTaskDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.Title))
        {
            return draft.Title.Trim();
        }

        var first = draft.Contents?
            .Split('\n')
            .Select(line => line.Trim(' ', '\t', '\r', '#'))
            .FirstOrDefault(line => line.Length > 0);

        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Length > 60 ? first[..60] + "…" : first;
        }

        return draft.TaskKey == 0 ? "제목 없는 새 작업" : $"#{draft.TaskKey}";
    }

    /// <summary>임시본 줄에 다는 꼬리말 — 어느 건인지와 본문 길이.</summary>
    private static string DraftMeta(AiTaskDraft draft)
    {
        var what = draft.TaskKey == 0 ? "저장 전 새 작업" : $"#{draft.TaskKey}";
        var chars = draft.Contents?.Length ?? 0;

        return chars == 0 ? what : $"{what} · {chars:N0}자";
    }

    /// <summary>주소로 건너와 고른 건이 있을 때의 임시본 처리. 읽기는 하되 저 혼자 다른 건을 열지 않는다(ResumeDraftAsync 를 부르면 링크를 누른 사람이 엉뚱한 건을 본다). 읽기를 건너뛰면 빈 채로 나간 다음 임시저장이 남은 임시본을 통째로 덮는다.</summary>
    private async Task ResumeLinkedDraftAsync()
    {
        await Drafts.ReadAsync();

        // 고를 때는 아직 못 읽은 상태였다. 그 건에 적어 둔 것이 있으면 이제 얹는다.
        if (_picked is { } row)
        {
            ApplyDraft(row);
        }
    }

    /// <summary>임시본을 버리고 서버에 있는 것으로 되돌린다. 새 작업이었으면 편집 자리를 비운다.</summary>
    private async Task DiscardDraftAsync()
    {
        if (_edit is null || _confirm is null)
        {
            return;
        }

        var ok = await _confirm.AskAsync(
            "적어 둔 임시본을 버립니다. 서버에 저장된 내용으로 돌아갑니다.",
            title: "임시본 버리기");

        if (!ok)
        {
            return;
        }

        await DropDraftAsync(_edit.TaskKey);

        if (_base is not null)
        {
            _edit = Copy(_base);
            return;
        }

        _edit = null;
        _picked = null;
    }

    /// <summary>서버가 돌려준 것을 편집 자리에 앉힌다. 견줄 바탕도 함께 바꾼다 — 안 바꾸면 저장 직후에도 「고친 것이 있다」가 되어 임시본이 되살아난다.</summary>
    private void Take(AiTaskDto? result)
    {
        if (result is null)
        {
            return;
        }

        _edit = result;
        _base = Copy(result);

        // 차수의 바탕이 바뀌었다 — 서버가 본문을 덮어 돌려주므로 다시 짓지 않으면 1차 자리에 옛 글이 남는다.
        TakeTurns();
    }

    /// <summary>서버에서 읽은 최신 상태를 왼쪽 표의 행에 옮긴다. 편집 자리는 행의 복사본(Copy)을 들므로 돌아가는 동안 바뀌는 값만 되돌리고, 제목·본문은 건드리지 않는다.</summary>
    private void SyncRow(AiTaskDto latest)
    {
        if (_rows.FirstOrDefault(r => r.TaskKey == latest.TaskKey) is not { } row)
        {
            return;
        }

        row.Title = latest.Title;
        row.TitleAuto = latest.TitleAuto;
        row.TitleRunKey = latest.TitleRunKey;
        row.TaskStatus = latest.TaskStatus;
        row.RequestFlag = latest.RequestFlag;
        row.RequestedAt = latest.RequestedAt;
        row.StartedAt = latest.StartedAt;
        row.FinishedAt = latest.FinishedAt;
        row.DurationMs = latest.DurationMs;
        row.AttemptCount = latest.AttemptCount;
        row.LastRunKey = latest.LastRunKey;
        row.LastExitCode = latest.LastExitCode;
        row.LastError = latest.LastError;
        row.UserConfirmed = latest.UserConfirmed;
    }

    /// <summary>표의 행과 편집 중인 것을 갈라 두기 위한 복사.</summary>
    private static AiTaskDto Copy(AiTaskDto s) => new()
    {
        TaskKey = s.TaskKey,
        FileCount = s.FileCount,
        Title = s.Title,
        TitleAuto = s.TitleAuto,
        TitleRunKey = s.TitleRunKey,
        Contents = s.Contents,
        ContentFormat = s.ContentFormat,
        TargetKey = s.TargetKey,
        TargetNm = s.TargetNm,
        TargetPath = s.TargetPath,
        TargetAllowPush = s.TargetAllowPush,
        TargetRunnerKinds = s.TargetRunnerKinds,
        TargetRef = s.TargetRef,
        RunnerKind = s.RunnerKind,
        RequestFlag = s.RequestFlag,
        TaskStatus = s.TaskStatus,
        Priority = s.Priority,
        TimeoutMinutes = s.TimeoutMinutes,
        AttemptCount = s.AttemptCount,
        AttemptMax = s.AttemptMax,
        AutoPush = s.AutoPush,
        NotifyEmail = s.NotifyEmail,
        NotifyPwa = s.NotifyPwa,
        NotifyTo = s.NotifyTo,
        NotifyWhen = s.NotifyWhen,
        NotifyError = s.NotifyError,
        UserConfirmed = s.UserConfirmed,
        IsUserRequest = s.IsUserRequest,
        RequestedAt = s.RequestedAt,
        StartedAt = s.StartedAt,
        FinishedAt = s.FinishedAt,
        DurationMs = s.DurationMs,
        LastRunKey = s.LastRunKey,
        LastExitCode = s.LastExitCode,
        LastError = s.LastError,
        PushedCommit = s.PushedCommit,
        PreviousTag = s.PreviousTag,
        RowVersion = s.RowVersion,
        CreId = s.CreId,
        CreDt = s.CreDt,
        ModId = s.ModId,
        ModDt = s.ModDt,
    };
}
