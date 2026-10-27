using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>AI 작업 「이어서 지시」 공통 팝업. 「빠른 지시」와 「AI 작업」이 완료된 작업에 추가 지시를 덧붙여 새로 요청할 때 쓴다.</summary>
public partial class AiTaskContinuePopup : IAsyncDisposable
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;
    [Inject] private AiDashboardClient Dashboard { get; set; } = default!;
    [Inject] private AiContinueDraftStore ContinueDrafts { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>이어서 지시할 대상 작업.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>이어서 지시 요청이 완료되어 새 작업/실행이 생성되었을 때 호출된다.</summary>
    [Parameter] public EventCallback<AiTaskDto> OnContinued { get; set; }

    /// <summary>팝업 닫힘 알림.</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    /// <summary>팝업 표시 여부 바인딩.</summary>
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    private bool _hasVisibleParam;
    private bool _visible;
    private bool _lastVisible;
    private bool _continuing;
    private string? _continueAddition;
    private string? _continueError;
    private string? _continueKind;
    private bool _hasSavedPending;

    /// <summary>진행 중인 작업 감시용 폴링 토큰</summary>
    private CancellationTokenSource? _pollCts;

    /// <summary>이전 확인된 상태</summary>
    private string? _lastItemStatus;

    /// <summary>마지막으로 처리한 작업 객체</summary>
    private AiTaskDto? _lastHandledItem;

    /// <summary>글상자를 가리키는 고유 ID. 한 화면에 팝업이 여럿이어도 충돌하지 않게 한다.</summary>
    private readonly string _continueDomId = $"pm-continue-{Guid.NewGuid():N}";
    private string ContinueSelector => $"#{_continueDomId}";

    private bool _continueRestored;
    private DateTime? _continueSavedAt;
    private bool _continueAttachPending;
    private long _continueTaskKey;

    /// <summary>Ctrl+Enter 를 받는 JS 모듈(빠른 지시 글상자와 같은 것).</summary>
    private IJSObjectReference? _submitModule;
    private DotNetObjectReference<AiTaskContinuePopup>? _submitRef;

    private IReadOnlyList<AiModelOption> _kinds = [];
    private bool _kindsRead;

    private IReadOnlyList<AiUsageSnapshot> _usage = [];
    private bool _usageSeen;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        _hasVisibleParam = parameters.TryGetValue<bool>(nameof(Visible), out _);
        await base.SetParametersAsync(parameters);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_hasVisibleParam && Visible != _lastVisible)
        {
            _lastVisible = Visible;
            if (Visible && !_visible)
            {
                if (Item is not null)
                {
                    await InitAsync(Item);
                }
                else
                {
                    _visible = true;
                }
            }
            else if (!Visible && _visible)
            {
                Close();
            }
        }
        else if (!_hasVisibleParam)
        {
            _lastVisible = _visible;
        }

        if (_visible && Item is { } current && current.TaskKey == _continueTaskKey && current != _lastHandledItem)
        {
            _lastHandledItem = current;
            await HandleStatusChangeAsync(current);
        }
    }

    private void OnCommVisibleChanged(bool visible)
    {
        if (!visible)
        {
            Close();
        }
        else
        {
            _visible = true;
            _lastVisible = true;
        }
    }

    /// <summary>지정된 작업을 대상으로 이어서 지시 팝업을 연다.</summary>
    public async Task OpenAsync(AiTaskDto item)
    {
        Item = item;
        await InitAsync(item);
    }

    private async Task InitAsync(AiTaskDto item)
    {
        _continueAddition = null;
        _continueError = null;
        _continueRestored = false;
        _continueSavedAt = null;
        _continueTaskKey = item.TaskKey;
        _lastItemStatus = item.TaskStatus;
        _lastHandledItem = item;
        _hasSavedPending = item.HasPendingContinue;

        if (_hasSavedPending && !string.IsNullOrWhiteSpace(item.PendingContinueAddition))
        {
            // 예약본에는 늘 붙는 문구가 들어 있다 — 걷어 보여 주고 저장할 때 다시 붙인다(두 번 붙지 않는다).
            _continueAddition = AiTaskAlways.Strip(item.PendingContinueAddition);
            if (!string.IsNullOrWhiteSpace(item.PendingContinueRunnerKind))
            {
                _continueKind = MatchKindCase(item.PendingContinueRunnerKind);
            }
        }

        if (!_kindsRead)
        {
            _kinds = await ModelCodes.GetAsync();
            _kindsRead = _kinds.Count > 0;
        }

        if (!_usageSeen)
        {
            await LoadUsageAsync();
        }

        if (!_hasSavedPending || string.IsNullOrWhiteSpace(_continueKind))
        {
            _continueKind = MatchKindCase(item.RunnerKind);
        }

        // 임시본 열쇠는 예약 여부와 상관없이 정한다 — 예약을 고치거나 지울 때도 같은 열쇠를 치운다.
        ContinueDrafts.Use(await MeAsync());

        if (_continueTaskKey > 0 && !_hasSavedPending)
        {
            if (await ContinueDrafts.ReadAsync(_continueTaskKey) is { } draft)
            {
                _continueAddition = draft.Addition;
                _continueSavedAt = draft.SavedAt;
                _continueRestored = true;

                if (!string.IsNullOrWhiteSpace(draft.RunnerKind))
                {
                    _continueKind = MatchKindCase(draft.RunnerKind);
                }
            }
        }

        _visible = true;
        _lastVisible = true;
        Visible = true;
        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(true);
        }

        _continueAttachPending = _continueTaskKey > 0;

        if (item.IsBusy)
        {
            StartPoll();
        }
        else
        {
            StopPoll();
        }

        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_continueAttachPending)
        {
            return;
        }

        _continueAttachPending = false;
        await ContinueDrafts.AttachAsync(ContinueSelector, _continueTaskKey);
        await AttachSubmitKeyAsync();
    }

    private async Task AttachSubmitKeyAsync()
    {
        try
        {
            _submitModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/ask-draft.js");
            _submitRef ??= DotNetObjectReference.Create(this);
            await _submitModule.InvokeVoidAsync("attachSubmitKey", ContinueSelector, _submitRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    /// <param name="text">누른 순간 글상자의 글. 회선에 아직 안 올라온 마지막 글자까지 담으려고 JS 가 함께 보낸다.</param>
    [JSInvokable]
    public async Task SubmitFromKeyAsync(string? text)
    {
        await InvokeAsync(async () =>
        {
            if (!_visible || _continuing)
            {
                return;
            }

            _continueAddition = string.IsNullOrWhiteSpace(text) ? null : text;

            // 단추가 꺼져 있을 자리(빈 글)에서는 보내지 않는다. 도는 작업이면 DoContinueAsync 가 예약으로 돌린다.
            if (!string.IsNullOrWhiteSpace(_continueAddition))
            {
                await DoContinueAsync();
            }

            StateHasChanged();
        });
    }

    /// <summary>작업 상태 전이를 감지하여 완료 시 자동 요청 또는 알림을 처리한다.</summary>
    private async Task HandleStatusChangeAsync(AiTaskDto fresh)
    {
        var prevStatus = _lastItemStatus;
        Item = fresh;
        _lastItemStatus = fresh.TaskStatus;
        _hasSavedPending = fresh.HasPendingContinue;

        if (prevStatus is "queued" or "preparing" or "running")
        {
            if (fresh.TaskStatus == "succeeded")
            {
                StopPoll();

                // 예약해 둔 것은 서버가 이어서 요청한다. 예약 없이 적어 둔 글은 그대로 두고 단추가 「바로 요청」으로 바뀐다.
                if (_hasSavedPending)
                {
                    Close();
                    if (OnContinued.HasDelegate)
                    {
                        await OnContinued.InvokeAsync(fresh);
                    }
                }
            }
            else if (fresh.IsContinuing || (fresh.TaskStatus == "queued" && fresh.LastRunKey is not null))
            {
                StopPoll();
                Close();
                if (OnContinued.HasDelegate)
                {
                    await OnContinued.InvokeAsync(fresh);
                }
            }
            else if (fresh.IsFinal && fresh.TaskStatus != "succeeded")
            {
                StopPoll();
                _continueError = _hasSavedPending
                    ? $"작업이 '{fresh.StatusText}'(으)로 끝나 예약한 이어서 지시가 실행되지 않았습니다."
                    : null;
            }
        }
        else if (!fresh.IsBusy)
        {
            StopPoll();
        }

        StateHasChanged();
    }

    private void StartPoll()
    {
        if (_pollCts is not null) return;
        var cts = new CancellationTokenSource();
        _pollCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested && _visible && _continueTaskKey > 0)
                {
                    await Task.Delay(2500, cts.Token);
                    if (cts.IsCancellationRequested || !_visible || _continueTaskKey <= 0) return;

                    var fresh = await Api.GetAsync(_continueTaskKey, cts.Token);
                    if (fresh is null || cts.IsCancellationRequested || !_visible) return;

                    await InvokeAsync(async () =>
                    {
                        if (!_visible || cts.IsCancellationRequested) return;
                        _lastHandledItem = fresh;
                        await HandleStatusChangeAsync(fresh);
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }, cts.Token);
    }

    private void StopPoll()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    /// <summary>팝업을 닫는다. 적어 둔 임시본은 보존된다.</summary>
    public void Close()
    {
        if (_continuing) return;

        Hide();
    }

    /// <summary>창을 닫는다. 요청이 끝난 자리에서도 불리므로 _continuing 을 보지 않는다 — 보면 「이어서 요청」 뒤에 비워진 창이 그대로 남는다.</summary>
    private void Hide()
    {
        StopPoll();

        _visible = false;
        _lastVisible = false;
        Visible = false;
        _continueAddition = null;
        _continueError = null;
        _continueKind = null;
        _continueRestored = false;
        _continueSavedAt = null;
        _hasSavedPending = false;

        if (VisibleChanged.HasDelegate)
        {
            _ = VisibleChanged.InvokeAsync(false);
        }

        if (OnClosed.HasDelegate)
        {
            _ = OnClosed.InvokeAsync();
        }

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        StopPoll();
        _submitRef?.Dispose();

        if (_submitModule is not null)
        {
            try
            {
                await _submitModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private async Task PickContinueKindAsync(string? kind)
    {
        _continueKind = kind;

        if (_continueTaskKey > 0)
        {
            await ContinueDrafts.SaveKindAsync(_continueTaskKey, kind);
        }
    }

    private async Task ClearContinueDraftAsync()
    {
        _continueAddition = null;
        _continueRestored = false;
        _continueSavedAt = null;

        if (_continueTaskKey > 0)
        {
            await ContinueDrafts.ClearAsync(ContinueSelector, _continueTaskKey);
        }
    }

    private async Task DoContinueAsync()
    {
        if (Item is not { } t || _continuing)
        {
            return;
        }

        if (t.IsBusy)
        {
            await SavePendingContinueAsync();
            return;
        }

        _continuing = true;
        _continueError = null;

        try
        {
            // 늘 따라붙는 문구를 붙여 전달한다
            var addition = AiTaskAlways.Append(_continueAddition);

            var updated = await Api.ContinueAsync(t.TaskKey, addition!, _continueKind);
            if (updated is not null)
            {
                updated = await Api.RequestAsync(t.TaskKey);
                await ContinueDrafts.ClearAsync(null, t.TaskKey);

                Hide();

                if (updated is not null)
                {
                    await OnContinued.InvokeAsync(updated);
                }
            }
        }
        catch (ApiException ex)
        {
            _continueError = $"이어서 지시 요청에 실패했습니다: {ex.Message}";
        }
        catch (Exception ex)
        {
            _continueError = $"이어서 지시 요청 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            _continuing = false;
        }
    }

    private async Task SavePendingContinueAsync()
    {
        if (Item is not { } t || _continuing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_continueAddition))
        {
            return;
        }

        _continuing = true;
        _continueError = null;

        try
        {
            var addition = AiTaskAlways.Append(_continueAddition);
            var updated = await Api.SetPendingContinueAsync(t.TaskKey, addition!, _continueKind);
            if (updated is not null)
            {
                Item = updated;
                await ContinueDrafts.ClearAsync(null, t.TaskKey);

                // 부모에게 알리기 전에 닫는다 — 부모가 다시 읽어 넘겨 준 건으로 창이 상태를 다시 셈하지 않게.
                Hide();

                if (OnContinued.HasDelegate)
                {
                    await OnContinued.InvokeAsync(updated);
                }
            }
        }
        catch (ApiException ex)
        {
            _continueError = $"자동 요청 예약에 실패했습니다: {ex.Message}";
        }
        catch (Exception ex)
        {
            _continueError = $"자동 요청 예약 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            _continuing = false;
        }
    }

    private async Task CancelPendingContinueAsync()
    {
        if (Item is not { } t || _continuing)
        {
            return;
        }

        _continuing = true;
        _continueError = null;

        try
        {
            await Api.ClearPendingContinueAsync(t.TaskKey);
            t.PendingContinueAddition = null;
            t.PendingContinueRunnerKind = null;
            t.PendingContinueUserId = null;
            t.PendingContinueAt = null;
            await ContinueDrafts.ClearAsync(null, t.TaskKey);

            Hide();

            if (OnContinued.HasDelegate)
            {
                await OnContinued.InvokeAsync(t);
            }
        }
        catch (ApiException ex)
        {
            _continueError = $"자동 요청 취소에 실패했습니다: {ex.Message}";
        }
        catch (Exception ex)
        {
            _continueError = $"자동 요청 취소 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            _continuing = false;
        }
    }

    private IReadOnlyList<AiModelOption> ContinueKinds
    {
        get
        {
            var allowed = (Item?.TargetRunnerKinds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            List<AiModelOption> picked = allowed.Length == 0
                ? [.. _kinds]
                : [.. _kinds.Where(k => allowed.Contains(k.Value, StringComparer.OrdinalIgnoreCase))];

            if (picked.Count == 0)
            {
                picked = [.. _kinds];
            }

            if (_continueKind is { Length: > 0 } now
                && !picked.Any(k => string.Equals(k.Value, now, StringComparison.OrdinalIgnoreCase)))
            {
                picked.Insert(0, new AiModelOption(
                    now,
                    _kinds.FirstOrDefault(
                        k => string.Equals(k.Value, now, StringComparison.OrdinalIgnoreCase))?.Text ?? now));
            }

            return picked;
        }
    }

    private string ContinueKindNote
    {
        get
        {
            if (_kinds.Count == 0)
            {
                return "AI 목록을 읽지 못해 지난 회차와 같은 AI 로 갑니다.";
            }

            if (ContinueKinds.Count <= 1)
            {
                return "이 대상은 AI 를 하나만 허용해 바꿀 수 없습니다.";
            }

            var same = string.Equals(_continueKind, Item?.RunnerKind, StringComparison.OrdinalIgnoreCase);

            return same
                ? string.Empty
                : $"지난 회차({KindName(Item?.RunnerKind)})와 다른 AI 에게 맡깁니다 — 지난 진행은 그대로 전달됩니다.";
        }
    }

    private string KindName(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "알 수 없음";
        }

        return _kinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Text ?? kind;
    }

    private string? MatchKindCase(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return kind;
        return _kinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Value ?? kind;
    }

    private List<AiUsageSnapshot> UsageOf(string? kind)
        => string.IsNullOrWhiteSpace(kind)
            ? []
            : [.. _usage.Where(u => string.Equals(u.RunnerKind, kind, StringComparison.OrdinalIgnoreCase))];

    private async Task LoadUsageAsync()
    {
        _usage = await Dashboard.UsageAsync();
        _usageSeen = true;
    }

    private async Task<string?> MeAsync() =>
        AuthState is null ? null : (await AuthState).User.Identity?.Name;
}
