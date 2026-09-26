using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using DevExpress.Blazor;
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

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(StatusFilters, o => o.Value, o => o.Text, _status),
        SchSummary.NameOf(FlagFilters, o => o.Value, o => o.Text, _flag),
        _targets.FirstOrDefault(t => t.TargetKey == _targetKey)?.TargetNm,
        SchSummary.NameOf(SourceFilters, o => o.Value, o => o.Text, _source),
        _keyword);

    /// <summary>고르는 칸 한 줄. 공통코드에 없는 값들이라 화면이 들고 있다.</summary>
    public sealed record PickOption(string Value, string Text);

    private static readonly PickOption[] StatusFilters =
    [
        new("", "전체"),
        new("idle", "작성중"), new("queued", "대기"), new("preparing", "준비중"),
        new("running", "실행중"), new("succeeded", "완료"), new("failed", "실패"),
        new("timeout", "시간초과"), new("canceled", "취소"), new("interrupted", "중단"),
    ];

    /// <summary>
    /// 그리드 칸별 필터 행에 표시할 목록.
    /// 칸별 필터(DxTagBox)에는 '전체'를 제외한 실제 상태 목록만 바인딩한다.
    /// </summary>
    private static readonly IReadOnlyList<PickOption> StatusFilterOptions =
        [.. StatusFilters.Where(x => !string.IsNullOrEmpty(x.Value))];

    private static readonly PickOption[] FlagFilters =
    [
        new("", "전체"),
        new("none", "안 함"), new("requested", "요청"), new("cancel_requested", "취소요청"),
    ];

    /// <summary>
    /// 누가 올린 건인가 — <c>ai_task.is_user_request</c>.
    /// </summary>
    /// <remarks>
    /// 값이 글자인 이유는 <c>DxComboBox</c> 가 「전체」를 고를 자리를 가져야
    /// 하기 때문이다. <c>bool?</c> 로 두면 세 상태를 표현할 수는 있지만
    /// 「전체」가 <c>null</c> 이 되어 <b>지우기 단추와 구별되지 않는다.</b>
    /// </remarks>
    private static readonly PickOption[] SourceFilters =
    [
        new("", "전체"),
        new("user", "사용자 요청"), new("admin", "직접 작성"),
    ];

    private IReadOnlyList<PickOption> RunnerKinds = [];

    private static readonly PickOption[] NotifyWhens =
    [
        new("always", "끝나면 언제나"), new("on_success", "성공했을 때만"),
        new("on_failure", "실패했을 때만"),
    ];

    private IReadOnlyList<AiTaskDto> _rows = [];
    private IReadOnlyList<AiTargetDto> _targets = [];
    private IReadOnlyList<AiTargetDto> _enabledTargets = [];

    private AiTaskDto? _picked;

    /// <summary>
    /// 오른쪽에서 고치는 것. <b>표의 행을 직접 고치지 않는다</b> —
    /// 저장하지 않고 다른 행으로 옮기면 표에 반쯤 고친 값이 남는다.
    /// </summary>
    private AiTaskDto? _edit;

    /// <summary>
    /// <b>서버가 들고 있는 그대로.</b> 「고친 것이 있나」를 이것과 견준다.
    /// </summary>
    /// <remarks>
    /// 왼쪽 표의 행(<see cref="_picked"/>)으로 견주면 <b>목록에 없는 건을
    /// 고칠 때</b> 기준이 사라진다 — 임시본을 이어 쓰느라 조건 밖의 건을
    /// 불러오는 길이 생겨서 따로 두었다.
    /// </remarks>
    private AiTaskDto? _base;

    private ConfirmDialog? _confirm;

    /// <summary>고른 작업의 실행 이력. 최근 것이 앞이다.</summary>
    private IReadOnlyList<AiTaskRunDto> _runs = [];

    /// <summary>지금 로그를 펼쳐 둔 실행.</summary>
    private AiTaskRunDto? _openRun;

    /// <summary>받아 둔 로그 줄. <b>그릴 때는 꼬리 500줄만</b> 쓴다.</summary>
    private readonly List<AiLogLineDto> _lines = [];

    /// <summary>마지막으로 받은 줄 번호. 다음에는 이것보다 큰 것만 달라고 한다.</summary>
    private int _lastSeq;

    /// <summary>
    /// 돌고 있는 동안만 도는 타이머.
    /// </summary>
    /// <remarks>
    /// <b>화면을 떠날 때 반드시 끈다.</b> 안 끄면 회로마다 타이머가 쌓이고,
    /// 사람이 화면을 열었다 닫을수록 서버가 느려진다.
    /// </remarks>
    private CancellationTokenSource? _tail;

    /// <summary>저장·요청이 오가는 중. 단추를 두 번 누르지 못하게 한다.</summary>
    private bool Busy;

    /// <summary>오른쪽 탭. 0 = 지시, 1 = 결과, 2 = 로그.</summary>
    private int _tab;

    /// <summary>
    /// 사람이 탭을 직접 만졌다. <b>그 뒤로는 화면이 저 혼자 안 넘긴다.</b>
    /// </summary>
    /// <remarks>
    /// 자동 전환은 「돌기 시작하면 실행 로그를 보여 준다」는 친절인데, 지시문을
    /// 고치려고 지시 탭을 열어 둔 사람에게 그것이 발동하면 <b>쓰던 글이
    /// 눈앞에서 사라진다.</b> 친절이 방해가 되는 자리다.
    /// 다른 건을 고르면 다시 켠다 — 그때는 새 맥락이다.
    /// </remarks>
    private bool _tabPinned;

    private void OnTabClick(TabClickEventArgs _) => _tabPinned = true;

    private int RunningCount => _rows.Count(t => t.IsBusy || t.TaskStatus is "running" or "preparing");

    private int SucceededCount => _rows.Count(t => t.TaskStatus == "succeeded");

    private int AttentionCount => _rows.Count(t => t.TaskStatus is "failed" or "timeout" or "interrupted");

    /// <summary>
    /// 돌기 시작했으면 로그 탭으로 넘긴다. <b>사람이 만진 뒤에는 안 넘긴다.</b>
    /// </summary>
    private void FollowRun()
    {
        if (!_tabPinned && _edit?.IsBusy == true)
        {
            _tab = 2;
        }
    }

    private string ListHint => $"{_rows.Count}건";

    private string EditTitle => _edit is null ? "내용"
        : _edit.TaskKey > 0 ? $"내용 · #{_edit.TaskKey}" : "내용 · 새 작업";

    private string EditHint => _edit is null ? string.Empty
        : _edit.TaskKey > 0 ? $"{_edit.StatusText}{(_edit.DurationText.Length > 0 ? $" · {_edit.DurationText}" : string.Empty)}"
        : "아직 저장하지 않았습니다";

    /// <summary>
    /// 1단계에서는 집어 가는 쪽이 없다. <b>그 사실을 화면이 말한다</b> —
    /// 「눌렀는데 아무 일도 안 난다」로 읽히지 않게.
    /// </summary>
    /// <remarks>
    /// <b>「대기」를 한 가지로 적지 않는다.</b> 안 집혀 간 것과 실패해서 다시
    /// 넣은 것은 상태값이 같아도(<c>queued</c>) 사람이 할 일이 다르다 —
    /// 앞엣것은 실행기를 봐야 하고, 뒤엣것은 지난 실패를 봐야 한다.
    /// </remarks>
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

    /// <summary>
    /// 고를 수 있는 AI. <b>대상이 허용한 것만</b> 남긴다(대상의 <c>RunnerKinds</c>).
    /// </summary>
    /// <remarks>
    /// 대상을 안 골랐으면 빈 목록이다 — 고를 수 없는 칸이 비어 있는 편이
    /// 「고를 수는 있는데 저장하면 틀리는」 것보다 낫다.
    /// <para>
    /// 대상이 <b>이 화면이 모르는 이름</b>을 들고 있으면 그 이름 그대로 한 줄을
    /// 세운다. 설정으로 CLI 가 늘어난 뒤 이 화면을 안 고쳤을 수 있고, 그때
    /// 목록에서 조용히 사라지면 왜 못 고르는지 알 길이 없다.
    /// </para>
    /// </remarks>
    private IReadOnlyList<PickOption> AllowedRunners
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
                RunnerKinds.FirstOrDefault(o => o.Value == v) ?? new PickOption(v, v))];
        }
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
        // **임시본은 사람마다 갈라 둔다.** 작업 번호는 모두의 것이라, 번호로만
        // 열쇠를 만들면 공용 PC 에서 남의 임시본이 내 화면에 뜬다.
        if (AuthState is not null)
        {
            var state = await AuthState;
            Drafts.Use(state.User.Identity?.Name);
        }

        await LoadModelsAsync();
        await LoadTargetsAsync();
        await SearchAsync();
    }

    private async Task LoadModelsAsync()
    {
        var models = await ModelCodes.GetAsync();
        RunnerKinds = [.. models.Select(x => new PickOption(x.Value, x.Text))];
    }

    /// <summary>
    /// 주소로 받은 작업 번호 (<c>?task=</c>).
    /// </summary>
    /// <remarks>
    /// 「빠른 지시」 화면에서 「자세히 보기」로 건너올 때 그 건의 번호를 실어
    /// 보낸다. 안 받으면 <b>맨 앞 건이 열린다</b>(표가 첫 줄을 골라 준다) —
    /// 방금 펼쳐 보던 것을 목록에서 다시 찾아야 하고, 그러면 링크를 걸어 둔
    /// 뜻이 없다.
    /// </remarks>
    [Parameter, SupplyParameterFromQuery(Name = "task")]
    public long? TaskKey { get; set; }

    /// <summary>
    /// <c>?key=</c> 형태로도 접근할 수 있게 한다. PWA 알림 등 외부에서
    /// 이 이름으로 링크를 보낼 수 있다. <see cref="TaskKey"/>가 없을 때만 쓴다.
    /// </summary>
    [Parameter, SupplyParameterFromQuery(Name = "key")]
    public long? KeyParam { get; set; }

    /// <summary>실제로 열어야 할 작업번호. <c>task</c> 우선, 없으면 <c>key</c>.</summary>
    private long? ResolvedTaskKey => TaskKey ?? KeyParam;

    /// <summary>
    /// 주소로 받아 이미 골라 준 번호. <b>같은 번호로 두 번 고르지 않는다</b> —
    /// 화면이 다시 그려질 때마다 고르면 사람이 옮겨 간 줄이 그 건으로 되돌아간다.
    /// </summary>
    private long? _linked;

    /// <summary>
    /// 주소가 지목한 건을 골라 준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>OnInitializedAsync</c> 가 아니라 여기다 — 이 화면이 탭에 이미 떠 있으면
    /// 두 번째로 건너올 때 <b>부품을 다시 만들지 않고 파라미터만 바꾼다.</b>
    /// 초기화에만 걸면 그때는 아무 일도 안 일어나고, 「자세히 보기」가 한 번만
    /// 듣는 링크가 된다.
    /// </para>
    /// <para>
    /// 초기화가 끝난 뒤에 불리므로 목록은 이미 읽혀 있다.
    /// </para>
    /// </remarks>
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
            // 조회 조건이 걸려 있어 목록에 없을 수 있다(탭을 열어 둔 채로 다시
            // 건너온 경우다). **조건을 지우고 한 번 더 본다** — 건너온 사람에게
            // 「없습니다」를 보이면 링크가 헛것이 된다.
            _status = null;
            _flag = null;
            _targetKey = null;
            _keyword = null;
            _source = null;

            // 그리드 칸별 필터도 함께 지운다.
            _statusFilterValues = [];
            _grid?.Grid?.ClearFilter();

            await SearchAsync();

            row = _rows.FirstOrDefault(r => r.TaskKey == key);
        }

        if (row is null)
        {
            // 그 사이 지워졌다. 목록은 그대로 두고 왜 안 열렸는지만 말해 준다.
            Say($"#{key} 작업을 찾지 못했습니다. 지워졌을 수 있습니다.", NoticeTone.Warning);
            return;
        }

        await PickAsync(row);

        // 그 줄이 둘째 쪽에 있을 수 있다. 고르기만 하면 오른쪽에는 떴는데
        // 왼쪽 표에는 켜진 줄이 안 보인다.
        _reveal = row;
    }

    /// <summary>다음 렌더에 표에서 보여 줄 줄. 쪽이 다르면 그 쪽으로 넘긴다.</summary>
    private AiTaskDto? _reveal;

    /// <summary>감싼 표. 쪽을 넘기려고 DevExpress API 를 직접 부른다.</summary>
    private CommGrd<AiTaskDto>? _grid;

    /// <summary>
    /// 고른 줄이 지금 쪽에 없으면 <b>그 줄이 있는 쪽으로 넘긴다.</b>
    /// </summary>
    /// <remarks>
    /// 자리 번호(<c>VisibleIndex</c>)는 <b>쪽 안의 자리가 아니라 전체에서의
    /// 자리</b>이고, 사람이 정렬을 바꿔 두었으면 목록 순서와도 다르다 —
    /// 그래서 <c>_rows</c> 에서 세지 않고 표에게 물어 찾는다.
    /// </remarks>
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

    /// <summary>
    /// 대상 목록. <b>조회 실패로 화면을 막지 않는다</b> — 대상이 없어도
    /// 작업 목록은 보여야 「왜 아무것도 안 보이나」를 알 수 있다.
    /// </summary>
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
        _rows = await Api.ListAsync(_status, _flag, _targetKey, _keyword, userRequest: UserRequestFilter);

        // 고른 것이 목록에서 빠졌으면 오른쪽을 비운다 — 없는 건을 고치고
        // 있다고 믿게 두지 않는다.
        if (_picked is not null && _rows.All(r => r.TaskKey != _picked.TaskKey))
        {
            _picked = null;

            // **적어 둔 것이 있으면 편집 자리는 그대로 둔다.** 조건을 바꿔
            // 목록에서 빠졌을 뿐인데 쓰던 글을 치우면 그것이 곧 사고다.
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

    /// <summary>고른 출처를 서버가 알아듣는 값으로. 「전체」는 조건 없음이다.</summary>
    private bool? UserRequestFilter => _source switch
    {
        "user" => true,
        "admin" => false,
        _ => null,
    };

    /// <summary>
    /// 그리드 칸별 필터 행 — 상태 다중 선택.
    /// 빈 목록은 「전체」(필터 없음)다.
    /// </summary>
    private IEnumerable<string> _statusFilterValues = [];

    /// <summary>
    /// 상태 다중 선택 필터가 바뀌었을 때.
    /// 선택된 값들을 <c>InOperator</c> 로 묶어 <c>context.FilterCriteria</c> 에 반영한다.
    /// 선택이 없으면(전체) <c>null</c> 로 기준을 지운다.
    /// </summary>
    private void OnStatusFilterChanged(
        IEnumerable<string> vals,
        GridDataColumnFilterRowCellTemplateContext filterCtx)
    {
        _statusFilterValues = vals;

        var list = vals.ToList();

        filterCtx.FilterCriteria = list.Count switch
        {
            0 => null,
            // 하나 — BinaryOperator 로 같음 비교. InOperator(1개) 보다 더 자연스럽다.
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
        _lines.Clear();
        _lastSeq = 0;

        // 새 건을 골랐으면 새 맥락이다. 고정을 풀고 탭도 「지시」로 되돌린다.
        _tabPinned = false;
        _tab = 0;

        if (item is null)
        {
            _draftAt = null;
            return;
        }

        // **고른 건에 적어 둔 것이 있으면 그것을 얹는다.** 표에서 다른 건에
        // 다녀오는 것도 「떠났다 돌아오는 것」이다.
        ApplyDraft(item);

        await LoadRunsAsync();

        // 돌고 있는 건을 골랐으면 **보고 싶은 것은 로그다.**
        FollowRun();

        // 돌고 있거나 방금 끝난 건은 **바로 펼친다.** 한 번 더 누르게 하면
        // 「눌렀는데 아무 일도 안 난다」로 읽힌다.
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
    }

    /// <summary>
    /// 그 실행의 로그를 펼친다. 돌고 있으면 <b>따라가기</b>를 시작한다.
    /// </summary>
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

        // 지시 탭(0)을 보고 있는 상태에서 실행 차수를 클릭했다면
        // 결과가 있으면 결과 탭(1), 없으면 로그 탭(2)으로 전환한다.
        if (_tab == 0)
        {
            _tab = !string.IsNullOrWhiteSpace(run.ResultText) ? 1 : 2;
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

            // 들고 있는 줄도 상한을 둔다. 수만 줄을 들고 있으면 회로가
            // 그것을 전부 실어 나른다.
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

    /// <summary>
    /// 작업이 도는 동안 따라간다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>실행이 아직 없을 때도 돈다.</b> 「요청」을 누른 직후에는 실행기가
    /// 집어 가기 전이라 실행 줄이 없는데, 그때 따라가기를 시작하지 않으면
    /// 화면이 「대기」에서 영영 안 움직인다 — 실제로는 30초 뒤에 돌고 있었다.
    /// </para>
    /// <para>
    /// 그래서 매 초 보는 것이 둘이다 — <b>작업 상태</b>(실행이 생겼나 · 끝났나)와
    /// <b>로그 꼬리</b>(펼쳐 둔 실행이 있으면).
    /// </para>
    /// </remarks>
    private void StartTail()
    {
        StopTail();

        _tail = new CancellationTokenSource();
        var ct = _tail.Token;

        // 따라가는 동안에는 가동 시간이 흐른다. 같은 토큰을 쓰므로
        // StopTail() 하나로 둘 다 멈춘다.
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

                        // **끝난 시각도 가져온다.** 「대기」가 방금 보낸 것인지
                        // 실패해서 다시 넣은 것인지를 이 값으로 가른다
                        // (<c>AiTaskDto.IsRetrying</c>) — 안 가져오면 재시도가
                        // 그냥 「대기」로 보인다.
                        _edit.FinishedAt = latest.FinishedAt;
                        _edit.LastError = latest.LastError;
                        _edit.NotifyError = latest.NotifyError;

                        // **왼쪽 표의 행도 함께 맞춘다.** `_edit` 는 표의 행과
                        // 일부러 갈라 둔 복사본이라(`Copy`), 여기만 고치면
                        // 오른쪽 「실행」 칸은 「완료」인데 왼쪽 배지는 「준비중」
                        // 인 채로 남는다 — 같은 건을 보면서 두 가지 상태를
                        // 읽게 된다.
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

                            changed = true;
                        }

                        // 다 끝났으면 따라가기를 멈춘다. 안 멈추면 회로가
                        // 영원히 2초마다 서버를 두들긴다.
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

    /// <summary>
    /// <b>1초마다 화면만 다시 그린다.</b> 서버에 묻지 않는다 — 가동 시간은
    /// 시작 시각과 지금으로 빼면 나오는 값이라, 이것 때문에 따라가기를
    /// 2초에서 1초로 당기면 <b>서버를 두 배로 두들기게 된다.</b>
    /// </summary>
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

    /// <summary>
    /// 화면에 그릴 로그. <b>꼬리 500줄만</b> 그린다 — 수만 줄을 DOM 에 그리면
    /// 회로가 그것을 전부 실어 나른다.
    /// </summary>
    private string _logText = string.Empty;

    /// <summary>로그 상자. JS 가 이것을 바닥으로 민다.</summary>
    private ElementReference _logEl;

    /// <summary>로그 따라가기(<c>js/log-tail.js</c>). 처음 쓸 때 싣는다.</summary>
    private IJSObjectReference? _logJs;

    /// <summary>
    /// 로그를 <b>바닥에 붙여 둔다.</b> 사람이 위를 읽는 중이면 JS 쪽이
    /// 알아서 놓아 둔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>로그 탭을 보고 있을 때만 부른다.</b> 숨어 있는 동안에는 상자 높이가
    /// 0 이라 밀어도 먹지 않고, 지시 탭에서 글을 치는 동안 글자마다 JS 를
    /// 왕복할 이유도 없다. 탭을 여는 것도 렌더라, 그때 이것이 불려 바닥으로 간다.
    /// </para>
    /// <para>
    /// 따라가기는 <b>편의다</b> — 못 해도 로그는 그대로 보인다. 그래서 여기서
    /// 나는 것으로 화면을 세우지 않는다(회로가 끊기는 중이면 JS 는 못 부른다).
    /// </para>
    /// </remarks>
    private async Task FollowLogAsync()
    {
        if (_openRun is null || _tab != 2)
        {
            return;
        }

        try
        {
            _logJs ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/log-tail.js");

            await _logJs.InvokeVoidAsync("follow", _logEl);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException or ObjectDisposedException)
        {
            // 화면을 떠났거나 회로가 끊겼다. 조용히 넘어간다.
        }
    }

    private string LogHint => _lines.Count == 0
        ? "아직 없습니다"
        : _lines.Count > 500 ? $"{_lines.Count}줄 중 마지막 500줄" : $"{_lines.Count}줄";

    private static string RunHint(AiTaskRunDto run) =>
        run.DiffStat is { Length: > 0 } d ? d.Split('\n')[^1].Trim() : $"exit {run.ExitCode}";

    /// <summary>
    /// 그 실행이 <b>얼마나 돌았나</b>. 돌고 있으면 지금까지, 끝났으면 걸린 만큼.
    /// </summary>
    /// <remarks>
    /// 시각은 DB 가 <c>now()</c> 로 찍은 시간대 없는 값이고 컨테이너는 전부
    /// <c>Asia/Seoul</c> 이라(<c>deploy/docker</c>) <see cref="DateTime.Now"/> 와
    /// 같은 시계다. <b>그래도 음수는 막는다</b> — 두 시계가 몇 초 어긋나면
    /// 방금 시작한 실행이 「-0:01」로 보인다.
    /// </remarks>
    private static string RunElapsedText(AiTaskRunDto run)
    {
        if (run.StartedAt is not { } started)
        {
            return string.Empty;
        }

        // 끝났으면 끝난 시각까지, 도는 중이면 지금까지. 끝나지도 않았는데
        // 도는 것도 아니면(취소·중단으로 끝시각이 안 남은 경우) 셀 것이 없다.
        var ended = run.FinishedAt ?? (run.IsRunning ? DateTime.Now : null);

        if (ended is not { } stop)
        {
            return string.Empty;
        }

        var span = stop - started;

        return ElapsedText(span < TimeSpan.Zero ? TimeSpan.Zero : span);
    }

    /// <summary>
    /// <c>3:07</c> · <c>1:02:03</c>. <b>한 시간을 넘겨야 시각 자리를 늘린다</b> —
    /// 대개는 몇 분이라 <c>0:03:07</c> 은 자리만 먹는다.
    /// </summary>
    private static string ElapsedText(TimeSpan span) =>
        span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";

    /// <summary>
    /// 대상이 바뀌면 <b>못 쓰는 AI 가 남아 있을 수 있다.</b> 그대로 두면
    /// 콤보는 빈칸으로 보이는데 값은 옛것이 실려 저장된다 — 화면과 자료가
    /// 어긋나는 쪽이라 여기서 바로잡는다.
    /// </summary>
    /// <summary>대상을 바꿨다. AI 와 「올리기」를 그 대상에 맞춘다.</summary>
    private void PickTarget(long? key)
    {
        if (_edit is null)
        {
            return;
        }

        _edit.TargetKey = key;
        FixRunnerForTarget();

        // 허용하지 않는 대상으로 옮기면 「올리기」가 켜진 채 남을 수 있다.
        // 서버도 저장할 때 끄지만, 화면에 켜진 것이 보이면 사람이 그렇게
        // 나갈 것이라고 믿는다.
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

        if (!allowed.Any(o => o.Value == _edit.RunnerKind))
        {
            _edit.RunnerKind = allowed[0].Value;
        }
    }

    /// <summary>
    /// 새 작업. 대상은 <b>하나뿐이면 미리 고른다</b> — 고를 것이 없는 목록을
    /// 열게 하지 않는다.
    /// </summary>
    /// <remarks>
    /// <b>메일 받기는 켠 채로 연다.</b> 이 화면의 일은 몇 분에서 몇십 분씩
    /// 걸리므로 사람이 그동안 화면을 붙들고 있지 않는다 — 꺼진 채로 두면
    /// 끝난 것을 아무도 모르고, 그 사실은 한참 뒤에야 드러난다.
    /// 받는 사람은 비워 둔다(요청한 사람에게 간다).
    /// </remarks>
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
        task.TimeoutMinutes = 60;
        task.AttemptMax = 3;
        task.NotifyEmail = true;
        task.NotifyWhen = "always";
        task.TargetKey = _targetKey ?? (_enabledTargets.Count == 1 ? _enabledTargets[0].TargetKey : null);
    }

    /// <summary>
    /// 표가 넘겨 준 줄을 오른쪽 편집 자리에 앉힌다(<c>OnEditOpen</c>).
    ///
    /// <para>
    /// <b>새로 만드는 것만 이리로 온다.</b> 편집 자리가 표 밖이라 관리 칸에
    /// 「수정」이 없고, 있는 건은 줄을 누르는 것이 곧 편집이다
    /// (<see cref="PickAsync"/>).
    /// </para>
    /// </summary>
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
        _tabPinned = false;
        _tab = 0;

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

        if (_grid is null)
        {
            return;
        }

        Busy = true;

        try
        {
            // 임시본의 열쇠다. 새 작업은 저장하면 번호가 붙으므로 **저장 전의
            // 번호**를 들고 있어야 그것을 버릴 수 있다.
            var draftKey = _edit.TaskKey;

            // **표의 저장 길로 되돌려 보낸다**(`OnSave` → `PersistAsync`).
            // 성공 문구도 목록을 다시 읽는 것도 표가 맡는다 — 화면마다
            // 「저장했습니다」가 조금씩 다른 말이 되지 않게.
            var saved = await _grid.SaveAsync(_edit, _edit.TaskKey == 0);

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

    /// <summary>
    /// 실제로 서버에 넣는 일. 표의 <c>OnSave</c> 가 이것을 부르고,
    /// 「요청」처럼 <b>저장부터 하고 가는 자리</b>도 같은 것을 부른다 —
    /// 등록이냐 수정이냐를 가르는 줄이 두 곳에 있으면 갈린다.
    /// </summary>
    private async Task PersistAsync((AiTaskDto Item, bool IsNew) e)
    {
        var result = e.IsNew
            ? await Api.CreateAsync(e.Item)
            : await Api.UpdateAsync(e.Item);

        Take(result);
    }

    /// <summary>
    /// AI 에게 시킨다. <b>먼저 저장한다</b> — 화면에 있는 글과 실행기가 집어
    /// 갈 글이 달라지는 것이 이 화면에서 제일 나쁜 어긋남이다.
    /// </summary>
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

                // **여기서 따라가기를 건다.** 안 걸면 화면이 「대기」에서
                // 안 움직이고, 사람은 눌린 것인지조차 알 수 없다.
                StartTail();

                // 방금 요청한 사람이 보고 싶은 것은 로그다. 이 한 번은
                // **고정을 무시하고 넘긴다** — 자기가 누른 결과라 화면이
                // 바뀌는 것이 놀랍지 않다.
                _tabPinned = false;
                _tab = 2;
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
            // 작업이 없어졌다. 그 임시본을 남겨 두면 다음에 화면을 열 때
            // **없는 건을 이어서 쓰려고 한다.**
            await DropDraftAsync(_edit.TaskKey);

            _picked = null;
            _edit = null;
            _base = null;
            await SearchAsync();
        }
    }

    private bool _continueOpen;
    private string? _addition;

    /// <summary>
    /// 이어서 시킬 수 있나. <b>끝난 건에만</b> — 돌고 있는 것에 얹으면
    /// 지금 도는 것과 다음에 돌 것이 뒤섞인다.
    /// </summary>
    private bool CanContinue => _edit is { TaskKey: > 0, IsBusy: false }
        && _runs.Count > 0;

    private Task AskContinueAsync()
    {
        _addition = null;
        _continueOpen = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 서버가 본문을 새로 짠다. <b>화면이 조립하지 않는다</b> —
    /// 지난 요약과 「기계가 잰 값」을 붙이는 규칙이 두 곳에 갈리면 안 된다.
    /// </summary>
    private async Task ContinueAsync()
    {
        if (_edit is null || string.IsNullOrWhiteSpace(_addition))
        {
            return;
        }

        Busy = true;

        try
        {
            var done = await RunAsync(
                async () =>
                {
                    var result = await Api.ContinueAsync(_edit!.TaskKey, _addition!);

                    Take(result);
                },
                "이어서 지시할 내용을 본문에 담았습니다. 확인하고 「요청」을 누르십시오.",
                "이어서 지시하지 못했습니다");

            if (done)
            {
                // 본문을 서버가 새로 짜 주었다. 그 앞의 임시본은 **지난 지시**라
                // 남겨 두면 다음에 그것이 새 본문을 덮는다.
                await DropDraftAsync(_edit!.TaskKey);

                _continueOpen = false;
                _addition = null;
                await SearchAsync();
                SyncPicked();
            }
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>저장한 뒤 왼쪽 표의 고른 행을 새 값으로 맞춘다.</summary>
    private void SyncPicked()
    {
        if (_edit is { TaskKey: > 0 })
        {
            _picked = _rows.FirstOrDefault(r => r.TaskKey == _edit.TaskKey);
        }
    }

    /// <summary>
    /// 고친 것이 있나. 요청할 때 헛저장을 한 번 줄이고,
    /// <b>임시저장할지 말지를 가른다.</b>
    /// </summary>
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

    /// <summary>
    /// 그릴 로그를 한 번만 만들고, <b>고친 것이 있으면 임시저장을 건다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 임시저장을 여기서 거는 이유는 <b>칸마다 변경 처리기를 달지 않으려고</b>다.
    /// 이 화면의 입력칸은 아홉이고 앞으로 더 는다 — 칸마다 달면 새 칸을
    /// 만드는 날 하나를 빠뜨리고, 그때 증상은 「그 칸만 조용히 안 적힌다」다.
    /// 렌더가 끝날 때마다 <b>지금 모양과 마지막으로 적은 모양을 견주면</b>
    /// 빠뜨릴 자리가 없다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var tail = _lines.Count <= 500 ? _lines : _lines.GetRange(_lines.Count - 500, 500);
        var text = string.Join('\n', tail.Select(l => l.Text));

        if (text != _logText)
        {
            _logText = text;
            StateHasChanged();
        }

        if (firstRender)
        {
            // **적어 둔 것은 회로가 붙은 뒤에야 읽을 수 있다**(JS 왕복).
            if (_linked is not null && _picked is not null)
            {
                await ResumeLinkedDraftAsync();
            }
            else
            {
                await ResumeDraftAsync();
            }

            RevealPicked();
            StateHasChanged();
            return;
        }

        RevealPicked();

        // 새 줄이 붙었든 탭을 방금 열었든, 지금 그려진 것의 바닥을 보여 준다.
        await FollowLogAsync();

        await TrackDraftAsync();
    }

    /// <summary>
    /// 화면을 떠난다. <b>타이머를 끄고, 기다리던 임시본을 지금 적는다.</b>
    /// </summary>
    /// <remarks>
    /// 타이머를 안 끄면 회로마다 쌓인다. 그리고 1.5초를 기다리던 임시본이
    /// 남아 있으면 <b>그것이 곧 잃어버리는 글</b>이다 — 탭을 옮기는 길에는
    /// 회로가 아직 살아 있으므로 여기서 적힌다. 창을 닫은 경우에는 적을 곳이
    /// 없고, 그때는 직전까지 적힌 것이 남는다.
    /// </remarks>
    public void Dispose()
    {
        StopTail();

        CancelDraftDelay();
        _ = WriteDraftAsync();

        // 실어 둔 JS 를 놓아 준다. 회로가 이미 끊긴 뒤면 놓을 것도 없다.
        if (_logJs is { } js)
        {
            _logJs = null;
            _ = ReleaseAsync(js);
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

    // ── 임시저장 ──────────────────────────────────────────────
    //
    // 무엇을 고친 것인지는 이 파일 머리말에, 어디에 어떻게 적는지는
    // `AiTaskDraftStore` 에 있다. 여기 있는 것은 **언제 적고 언제 버리는가**다.

    /// <summary>
    /// 화면을 열 때 <b>말없이 이어서 열어 주는</b> 기한.
    /// </summary>
    /// <remarks>
    /// 이 기한이 없으면 사흘 전에 적어 둔 것이 <b>화면을 열 때마다</b> 열린다 —
    /// 다른 일을 하러 온 사람에게는 그것이 방해다. 지난 것은 열지 않고
    /// 목록의 「임시」 표와 한 줄 안내로만 알린다(줄을 누르면 그때 얹힌다).
    /// </remarks>
    private static readonly TimeSpan ResumeWindow = TimeSpan.FromHours(12);

    /// <summary>입력이 이만큼 멈추면 적어 둔다.</summary>
    /// <remarks>
    /// 편집기가 이미 300ms 를 기다렸다가 값을 올린다(<c>CodeEditor</c>).
    /// 여기서 한 번 더 기다리는 이유는 <b>왕복이 브라우저까지 가기</b> 때문이다 —
    /// 타자 속도로 적으면 회로를 두들기게 된다. 합쳐서 2초 안쪽이라, 실수로
    /// 창을 닫아도 잃는 것은 마지막 한두 문장이다.
    /// </remarks>
    private const int DraftDelayMs = 1500;

    /// <summary>
    /// 마지막으로 적어 둔 임시본의 모양. <b>같은 것을 두 번 적지 않는다</b> —
    /// 따라가기가 2초마다 화면을 다시 그리므로 견주지 않으면 그때마다 적는다.
    /// </summary>
    private string? _draftShape;

    /// <summary>
    /// 지금 고치는 것이 임시저장된 때. <c>null</c> 이면 임시본이 없다 —
    /// 안내 줄과 「임시본 버리기」 단추가 이 값 하나를 본다.
    /// </summary>
    private DateTime? _draftAt;

    /// <summary>적기를 기다리는 중. 새 입력이 오면 이전 대기를 취소한다.</summary>
    private CancellationTokenSource? _draftDelay;

    /// <summary>기다리는 중인 것. 화면을 떠날 때 이것을 바로 적는다.</summary>
    private AiTaskDraft? _pendingDraft;

    /// <summary>
    /// 기다리는 중인 것의 작업 번호. <see cref="_pendingDraft"/> 가 <c>null</c>
    /// 이면 <b>버리라는 뜻</b>이다(고친 것이 없어졌다).
    /// </summary>
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

    /// <summary>
    /// 고친 것이 있으면 잠시 뒤에 적어 두고, 없어졌으면 적어 둔 것을 버린다.
    /// 렌더가 끝날 때마다 불린다.
    /// </summary>
    private async Task TrackDraftAsync()
    {
        // **기다리던 것이 다른 건의 것이면 먼저 적는다.** 그냥 덮으면, 적기를
        // 기다리는 1.5초 안에 다른 줄을 누른 사람의 마지막 몇 문장이 사라진다 —
        // 그것을 막으려고 만든 기능에서 그것이 나면 안 된다.
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

        // 새 작업은 **무엇이라도 적혀 있을 때만** 담는다. 「새 작업」을 눌러
        // 놓고 그냥 떠난 빈 칸을 담으면, 다음에 화면을 열 때마다 빈 새 작업이
        // 열린다.
        var worth = _edit.TaskKey > 0
            || !string.IsNullOrWhiteSpace(_edit.Contents)
            || !string.IsNullOrWhiteSpace(_edit.Title);

        var draft = HasChanges() && worth ? DraftOf(_edit) : null;

        // 적을 때가 되어야 시각을 찍으므로(`SaveAsync`) 이 모양은 내용이
        // 그대로면 그대로다 — 견주는 값으로 쓸 수 있는 까닭이다.
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

                // **회로의 차례를 빌려 적는다.** JS 왕복은 렌더 순서 안에서
                // 일어나야 한다.
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

        // 적은 뒤에 딴 건으로 옮겨 갔을 수 있다. 그때 이 시각을 그대로 걸면
        // **엉뚱한 건이 임시저장된 것처럼 보인다.**
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

    /// <summary>
    /// 그 건에 적어 둔 것이 있으면 편집 자리에 얹는다.
    /// </summary>
    /// <remarks>
    /// <b>도는 중인 건에는 얹지 않는다</b> — 고칠 수 없는 화면에 고친 글을
    /// 띄우면 화면의 글과 실행기가 들고 간 글이 다시 어긋난다.
    /// </remarks>
    private void ApplyDraft(AiTaskDto server)
    {
        _draftAt = null;

        if (_edit is null || server.IsBusy || !Drafts.Items.TryGetValue(server.TaskKey, out var draft))
        {
            return;
        }

        Overlay(draft);

        // 그 사이 누군가 저장했다. 그대로 저장하면 그 사람의 글이 덮인다 —
        // **말해 주는 데서 멈춘다.** 여기서 임시본을 버리면 사람이 적은 것이
        // 사라지고, 자동으로 합치면 둘 다 아닌 글이 된다.
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

    /// <summary>
    /// 화면이 다시 떴다. <b>적어 둔 것이 있으면 가장 최근 것을 이어서 연다.</b>
    /// 탭을 옮겼다 돌아온 사람이 보게 되는 길이 이것이다.
    /// </summary>
    private async Task ResumeDraftAsync()
    {
        var drafts = await Drafts.ReadAsync();

        if (drafts.Count == 0)
        {
            return;
        }

        var latest = drafts.Values.OrderByDescending(d => d.SavedAt).First();

        // 오래된 것은 열지 않는다. 있다는 것만 알린다 — 목록에는 「임시」 표가
        // 붙어 있으므로 누르면 그때 얹힌다.
        if (DateTime.Now - latest.SavedAt > ResumeWindow)
        {
            Say($"적어 둔 임시본이 {drafts.Count}건 있습니다. 목록의 「임시」 표시를 누르면 이어서 쓸 수 있습니다.");
            return;
        }

        if (latest.TaskKey == 0)
        {
            NewTask();
            Overlay(latest);
            Say($"적어 두었던 새 작업을 이어서 씁니다 ({latest.SavedAt:MM-dd HH:mm}).");
            return;
        }

        // 조건이 걸려 있으면 목록에 없을 수 있다. 그때는 한 건만 따로 읽는다 —
        // **쓰던 글이 조회 조건 때문에 안 열리면 안 된다.**
        var row = _rows.FirstOrDefault(r => r.TaskKey == latest.TaskKey)
            ?? await SafeGetAsync(latest.TaskKey);

        if (row is null)
        {
            // 그 작업이 지워졌다. 임시본도 함께 버린다.
            await Drafts.RemoveAsync(latest.TaskKey);
            return;
        }

        await PickAsync(row);

        // 도는 중이었으면 얹지 않았다(`ApplyDraft`). 그때는 말도 하지 않는다.
        if (_draftAt is not null)
        {
            var name = string.IsNullOrWhiteSpace(_edit?.Title) ? $"#{latest.TaskKey}" : _edit!.Title;
            Say($"「{name}」 에 적어 두었던 내용을 이어서 씁니다 ({latest.SavedAt:MM-dd HH:mm}).");
        }
    }

    /// <summary>
    /// 주소로 건너와 고른 건이 있을 때의 임시본 처리.
    /// <b>읽기는 하되 저 혼자 다른 건을 열지는 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「자세히 보기」로 건너온 사람이 보려는 것은 <b>그 건</b>이지, 지난번에
    /// 쓰다 만 다른 건이 아니다. <see cref="ResumeDraftAsync"/> 를 그대로 부르면
    /// 가장 최근 임시본 쪽으로 화면이 옮겨 가 버려, 링크를 눌러 온 사람이
    /// 엉뚱한 건을 보게 된다.
    /// </para>
    /// <para>
    /// 그렇다고 읽기까지 건너뛰면 안 된다 — 들고 있는 것이 빈 채로 다음
    /// 임시저장이 나가면 <b>남아 있던 임시본이 통째로 덮인다</b>
    /// (<see cref="AiTaskDraftStore"/> 는 한 열쇠에 전부 담는다).
    /// </para>
    /// </remarks>
    private async Task ResumeLinkedDraftAsync()
    {
        await Drafts.ReadAsync();

        // 고를 때는 아직 못 읽은 상태였다. 그 건에 적어 둔 것이 있으면 이제 얹는다.
        if (_picked is { } row)
        {
            ApplyDraft(row);
        }
    }

    /// <summary>
    /// 임시본을 버리고 <b>서버에 있는 것으로 되돌린다.</b> 새 작업이었으면
    /// 되돌릴 것이 없으므로 편집 자리를 비운다.
    /// </summary>
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

    /// <summary>
    /// 서버가 돌려준 것을 편집 자리에 앉힌다. <b>견줄 바탕도 함께 바꾼다</b> —
    /// 안 바꾸면 저장한 직후에도 「고친 것이 있다」가 되어 임시본이 되살아난다.
    /// </summary>
    private void Take(AiTaskDto? result)
    {
        if (result is null)
        {
            return;
        }

        _edit = result;
        _base = Copy(result);
    }

    /// <summary>
    /// 서버에서 읽어 온 최신 상태를 <b>왼쪽 표의 행</b>에 옮긴다.
    ///
    /// <para>
    /// 표는 <see cref="_rows"/> 를 그리고 편집 자리는 그 <b>복사본</b>을 든다
    /// (<see cref="Copy"/>). 갈라 둔 것은 일부러다 — 고치다 만 글이 목록에
    /// 새어 나가면 안 된다. 그래서 <b>돌아가는 동안 바뀌는 값만</b> 골라
    /// 되돌려 준다. 제목·본문 같은 사람이 고치는 값은 건드리지 않는다.
    /// </para>
    ///
    /// <para>
    /// 조회 조건에 걸려 목록에 없는 건일 수 있다. 그때는 할 일이 없다.
    /// </para>
    /// </summary>
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
    }

    /// <summary>표의 행과 편집 중인 것을 갈라 두기 위한 복사.</summary>
    private static AiTaskDto Copy(AiTaskDto s) => new()
    {
        TaskKey = s.TaskKey,
        Title = s.Title,
        TitleAuto = s.TitleAuto,
        TitleRunKey = s.TitleRunKey,
        Contents = s.Contents,
        ContentFormat = s.ContentFormat,
        TargetKey = s.TargetKey,
        TargetNm = s.TargetNm,
        TargetPath = s.TargetPath,
        TargetAllowPush = s.TargetAllowPush,
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
        NotifyTo = s.NotifyTo,
        NotifyWhen = s.NotifyWhen,
        NotifyError = s.NotifyError,
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
