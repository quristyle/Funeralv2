using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// AI 작업 「이어서 지시」 공통 팝업.
/// <para>
/// 「빠른 지시」( <see cref="AiTaskActions"/> )와 「AI 작업」( <c>AiTaskList</c> )에서
/// 완료된 작업에 추가 지시를 덧붙여 새로 요청할 때 사용하는 공통 컴포넌트다.
/// </para>
/// </summary>
public partial class AiTaskContinuePopup
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;
    [Inject] private AiDashboardClient Dashboard { get; set; } = default!;
    [Inject] private AiContinueDraftStore ContinueDrafts { get; set; } = default!;

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

    private bool _visible;
    private bool _lastVisible;
    private bool _continuing;
    private string? _continueAddition;
    private string? _continueError;
    private string? _continueKind;

    /// <summary>
    /// 글상자를 가리키는 고유 ID. 한 화면에 여러 팝업이 존재해도 충돌하지 않도록 한다.
    /// </summary>
    private readonly string _continueDomId = $"pm-continue-{Guid.NewGuid():N}";
    private string ContinueSelector => $"#{_continueDomId}";

    private bool _continueRestored;
    private DateTime? _continueSavedAt;
    private bool _continueAttachPending;
    private long _continueTaskKey;

    private IReadOnlyList<AiModelOption> _kinds = [];
    private bool _kindsRead;

    private IReadOnlyList<AiUsageSnapshot> _usage = [];
    private bool _usageSeen;

    protected override async Task OnParametersSetAsync()
    {
        if (Visible != _lastVisible)
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

        if (!_kindsRead)
        {
            _kinds = await ModelCodes.GetAsync();
            _kindsRead = _kinds.Count > 0;
        }

        if (!_usageSeen)
        {
            await LoadUsageAsync();
        }

        _continueKind = MatchKindCase(item.RunnerKind);

        if (_continueTaskKey > 0)
        {
            ContinueDrafts.Use(await MeAsync());

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
        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(true);
        }

        _continueAttachPending = _continueTaskKey > 0;
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
    }

    /// <summary>팝업을 닫는다. 적어 둔 임시본은 보존된다.</summary>
    public void Close()
    {
        if (_continuing) return;

        _visible = false;
        _lastVisible = false;
        _continueAddition = null;
        _continueError = null;
        _continueKind = null;
        _continueRestored = false;
        _continueSavedAt = null;

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
                _visible = false;
                _lastVisible = false;

                _continueRestored = false;
                _continueSavedAt = null;
                await ContinueDrafts.ClearAsync(ContinueSelector, t.TaskKey);

                if (VisibleChanged.HasDelegate)
                {
                    await VisibleChanged.InvokeAsync(false);
                }

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
                ? "지난 회차와 같은 AI 가 이어서 처리합니다."
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

    private AiUsageText.Badge KindBadge(string? kind)
        => _usageSeen
            ? AiUsageText.Of(UsageOf(kind))
            : new AiUsageText.Badge("한도 확인 중", "jsini-badge--off");

    private AiUsageText.Badge PickedContinueBadge => KindBadge(_continueKind);

    private async Task LoadUsageAsync()
    {
        _usage = await Dashboard.UsageAsync();
        _usageSeen = true;
    }

    private async Task<string?> MeAsync() =>
        AuthState is null ? null : (await AuthState).User.Identity?.Name;
}
