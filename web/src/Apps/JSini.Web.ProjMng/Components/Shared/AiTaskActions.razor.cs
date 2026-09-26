using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class AiTaskActions
{
    [Inject] private AiTaskClient Api { get; set; } = default!;
    [Inject] private AiModelCodes ModelCodes { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private AiTaskDraftStore Drafts { get; set; } = default!;
    [Inject] private AiContinueDraftStore ContinueDrafts { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>단추가 걸릴 건. 부모가 갈아 준다.</summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>요청이 나갔다(재시도 · 이어서 지시). 부모가 목록을 다시 읽는다.</summary>
    [Parameter] public EventCallback<AiTaskDto> OnChanged { get; set; }

    /// <summary>
    /// 사용자확인이 끝났다. 창은 <b>닫히고</b> 화면은 그 자리에 남으므로
    /// <see cref="OnChanged"/> 와 따로 둔다.
    /// </summary>
    [Parameter] public EventCallback<AiTaskDto> OnConfirmed { get; set; }

    /// <summary>
    /// 작성중인 건을 지웠다. <b>부모가 뒷일을 정한다</b> — 창은 닫고 목록을
    /// 다시 읽으면 되지만, 탭으로 열린 화면은 주소가 가리킬 것이 없어져
    /// 탭째 닫는다.
    /// </summary>
    [Parameter] public EventCallback<AiTaskDto> OnDeleted { get; set; }

    /// <summary>
    /// 「자세히 보기」를 그리나. <b>「AI 작업」 화면으로 보내는 단추</b>라
    /// 창에서만 뜻이 있다 — 탭으로 열린 화면은 이미 큰 화면이다.
    /// </summary>
    [Parameter] public bool ShowDetail { get; set; } = true;

    private bool _retryOpen;
    private bool _retrying;
    private string? _retryAddition;
    private string? _retryError;

    private bool _continueOpen;
    private bool _continuing;
    private string? _continueAddition;
    private string? _continueError;

    /// <summary>
    /// 이 부품의 글상자를 가리키는 아이디. <b>인스턴스마다 다르다</b> —
    /// 창과 탭 화면이 함께 떠 있으면 상자가 둘이라 이름으로는 못 가른다.
    /// </summary>
    private readonly string _continueDomId = $"pm-continue-{Guid.NewGuid():N}";

    private string ContinueSelector => $"#{_continueDomId}";

    /// <summary>적다 만 것을 되살렸나. 참이면 글상자 아래에 한 줄이 선다.</summary>
    private bool _continueRestored;

    /// <summary>되살린 것이 언제 적힌 것인가.</summary>
    private DateTime? _continueSavedAt;

    /// <summary>
    /// 창이 열려 다시 그려지기를 기다리는 중. <b>글상자가 DOM 에 선 뒤에야</b>
    /// 실시간 감시를 걸 수 있어 한 판 미룬다.
    /// </summary>
    private bool _continueAttachPending;

    /// <summary>임시본을 건 작업 번호. 창을 닫아도 버리지 않으므로 들고 있는다.</summary>
    private long _continueTaskKey;

    /// <summary>
    /// 공통코드(<c>AI_MODEL</c>)에서 읽은 AI 목록. <b>창을 처음 열 때 한 번 읽는다</b> —
    /// 단추만 서 있는 동안에도 읽으면 이 부품이 놓인 자리마다(창 · 탭 화면)
    /// 게이트웨이를 한 번씩 두드린다.
    /// </summary>
    private IReadOnlyList<AiModelOption> _kinds = [];

    /// <summary>목록을 이미 읽었나. 못 읽었을 때 다시 읽게 하려고 따로 둔다.</summary>
    private bool _kindsRead;

    /// <summary>
    /// 이 회차를 맡을 AI. <b>창을 열 때 지난 회차의 것으로 채운다.</b>
    /// </summary>
    private string? _continueKind;

    private bool _confirming;

    private bool _deleting;

    /// <summary>지우기 전에 묻는 창.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>지금 로그인한 사람. 남의 건인지 가리는 데 쓴다.</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private string? _retryKind;

    private async Task OpenRetryModalAsync()
    {
        _retryAddition = null;
        _retryError = null;
        _retryKind = Item?.RunnerKind;

        if (!_kindsRead)
        {
            _kinds = await ModelCodes.GetAsync();
            _kindsRead = _kinds.Count > 0;
        }

        _retryOpen = true;
    }

    private void CloseRetryModal()
    {
        if (_retrying) return;
        _retryOpen = false;
        _retryAddition = null;
        _retryError = null;
        _retryKind = null;
    }

    private async Task DoRetryAsync()
    {
        if (Item is not { } t || _retrying)
        {
            return;
        }

        _retrying = true;
        _retryError = null;

        try
        {
            var updated = await Api.RetryAsync(t.TaskKey, _retryAddition, _retryKind);
            _retryOpen = false;

            if (updated is not null)
            {
                await OnChanged.InvokeAsync(updated);
            }
        }
        catch (ApiException ex)
        {
            _retryError = $"재시도 요청에 실패했습니다: {ex.Message}";
        }
        catch (Exception ex)
        {
            _retryError = $"재시도 요청 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            _retrying = false;
        }
    }

    /// <summary>
    /// 고를 수 있는 AI. <b>대상이 허용한 것만</b> 남긴다 — 「빠른 지시」 화면의
    /// 같은 규칙이고(<c>AiAsk.AllowedKinds</c>) 서버도 저장할 때 같은 것을 본다.
    /// </summary>
    /// <remarks>
    /// <b>못 걸러졌으면 전부 내놓는다.</b> 허용 목록이 비어 있거나(옛 대상)
    /// 공통코드와 하나도 안 맞으면 걸러 낸 결과가 빈 목록이 되는데, 그러면
    /// 칸이 비어 <b>지금 어느 AI 로 가는지조차 안 보인다.</b>
    /// <para>
    /// 지난 회차가 쓴 AI 가 그 목록에 없을 수도 있다 — 그 뒤로 대상의 허용
    /// 목록이 줄었거나 공통코드에서 빠진 경우다. 그때는 <b>그 값을 한 줄로
    /// 끼워 넣는다</b>(이름은 코드값 그대로). 안 끼우면 칸이 빈 채로 열려
    /// 「바꾸지 않고 보내기」를 할 수 없다.
    /// </para>
    /// </remarks>
    private IReadOnlyList<AiModelOption> RetryKinds
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

            if (_retryKind is { Length: > 0 } now
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

    private string RetryKindNote
    {
        get
        {
            if (_kinds.Count == 0)
            {
                return "AI 목록을 읽지 못해 지난 회차와 같은 AI 로 갑니다.";
            }

            if (RetryKinds.Count <= 1)
            {
                return "이 대상은 AI 를 하나만 허용해 바꿀 수 없습니다.";
            }

            var same = string.Equals(_retryKind, Item?.RunnerKind, StringComparison.OrdinalIgnoreCase);

            return same
                ? "지난 회차와 같은 AI 로 재시도합니다."
                : $"지난 회차({KindName(Item?.RunnerKind)})와 다른 AI 에게 맡깁니다 — 지난 진행은 그대로 전달됩니다.";
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

    /// <summary>
    /// 칸 아래 한 줄. <b>갈아탔는지를 말한다</b> — 목록에서 고르는 동작은
    /// 되돌리기 쉽지만, 고른 것을 잊은 채로 보내는 것은 되돌릴 수 없다.
    /// </summary>
    private string ContinueKindNote
    {
        get
        {
            // **못 읽은 것과 하나뿐인 것을 가른다.** 목록을 못 받았을 때도
            // 칸에는 지난 회차의 AI 한 줄이 서므로 「하나만 허용한다」로
            // 적으면 대상 설정을 들여다보게 만든다.
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

    /// <summary>사람이 읽는 AI 이름. 못 찾으면 코드값을 그대로 적는다.</summary>
    private string KindName(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "알 수 없음";
        }

        return _kinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Text ?? kind;
    }

    private async Task OpenContinueModalAsync()
    {
        _continueAddition = null;
        _continueError = null;
        _continueRestored = false;
        _continueSavedAt = null;
        _continueTaskKey = Item?.TaskKey ?? 0;

        // **지난 회차의 AI 로 채운다.** 열자마자 다른 것이 골라져 있으면
        // 바꾼 줄 모르고 보내는 쪽이 생긴다(머리말).
        _continueKind = Item?.RunnerKind;

        if (!_kindsRead)
        {
            // 목록을 못 읽어도 창은 연다 — `AiModelCodes` 는 실패를 빈 목록으로
            // 돌려주고, 그때는 지난 회차의 AI 한 줄만 선다(`ContinueKinds`).
            _kinds = await ModelCodes.GetAsync();
            _kindsRead = _kinds.Count > 0;
        }

        // 적다 만 것이 있으면 되살린다. **못 읽어도 창은 연다** — 임시저장이
        // 없는 것은 불편이고, 여기서 막으면 이어서 지시 자체를 못 한다.
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
                    _continueKind = draft.RunnerKind;
                }
            }
        }

        _continueOpen = true;

        // 글상자는 지금 DOM 에 없다. 이 판이 그려진 뒤에 건다.
        _continueAttachPending = _continueTaskKey > 0;
    }

    /// <summary>
    /// 창이 그려진 뒤에 글상자를 잡는다. <b>열 때마다 다시 건다</b> —
    /// DevExpress 가 창을 닫으면서 글상자를 DOM 에서 들어내기도 해서,
    /// 한 번 걸어 두는 것으로는 두 번째로 연 창이 안 적힌다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_continueAttachPending)
        {
            return;
        }

        _continueAttachPending = false;

        await ContinueDrafts.AttachAsync(ContinueSelector, _continueTaskKey);
    }

    /// <summary>
    /// 창을 닫는다. <b>적어 둔 것은 그대로 둔다</b> — 어떻게 닫았는지로
    /// 살리고 버리기를 가르면 그 기억이 사람 몫이 된다(머리말).
    /// </summary>
    private void CloseContinueModal()
    {
        if (_continuing) return;
        _continueOpen = false;
        _continueAddition = null;
        _continueError = null;
        _continueKind = null;
        _continueRestored = false;
        _continueSavedAt = null;
    }

    /// <summary>
    /// 맡을 AI 를 골랐다. <b>고른 것도 적어 둔다</b> — 글만 살아 돌아오고
    /// AI 가 지난 회차 것으로 되돌아가면 갈아탄 줄 알고 보내게 된다.
    /// </summary>
    private async Task PickContinueKindAsync(string? kind)
    {
        _continueKind = kind;

        if (_continueTaskKey > 0)
        {
            await ContinueDrafts.SaveKindAsync(_continueTaskKey, kind);
        }
    }

    /// <summary>되살린 것을 사람이 직접 버린다. 적어 둔 것과 글상자를 함께 비운다.</summary>
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

    /// <summary>지금 로그인한 사람의 아이디. 임시본 열쇠와 「남의 건인가」에 쓴다.</summary>
    private async Task<string?> MeAsync() =>
        AuthState is null ? null : (await AuthState).User.Identity?.Name;

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
            // 늘 따라붙는 문구는 「빠른 지시」와 한 곳에서 나온다
            // (`AiTaskAlways`) — 두 군데 적어 두면 한쪽만 고쳐진다.
            var addition = AiTaskAlways.Append(_continueAddition);

            var updated = await Api.ContinueAsync(t.TaskKey, addition!, _continueKind);
            if (updated is not null)
            {
                updated = await Api.RequestAsync(t.TaskKey);
                _continueOpen = false;

                // 서버가 받아 갔으니 적어 둔 것은 제 할 일을 다했다. **여기서만
                // 저절로 버린다** — 창을 닫는 다른 길은 모두 남겨 둔다(머리말).
                _continueRestored = false;
                _continueSavedAt = null;
                await ContinueDrafts.ClearAsync(ContinueSelector, t.TaskKey);

                if (updated is not null)
                {
                    await OnChanged.InvokeAsync(updated);
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

    private async Task DoConfirmAsync()
    {
        if (Item is not { } t || _confirming)
        {
            return;
        }

        _confirming = true;

        try
        {
            var updated = await Api.ConfirmAsync(t.TaskKey);

            if (updated is not null)
            {
                await OnConfirmed.InvokeAsync(updated);
            }
        }
        catch (Exception)
        {
            // 확인 표시 하나 때문에 화면을 깨지 않는다. 다음에 다시 누르면 된다.
        }
        finally
        {
            _confirming = false;
        }
    }

    /// <summary>
    /// 작성중인 건을 지운다. <b>묻고 나서</b> 지운다 — 되돌릴 방법이 화면에 없다.
    /// </summary>
    private async Task AskDeleteAsync()
    {
        if (Item is not { IsDraft: true } t || _confirm is null || _deleting)
        {
            return;
        }

        var me = await MeAsync();

        // 남의 건이면 누구 것인지 적는다. 「빠른 지시」는 모두의 카드를 보여 주므로
        // 내 것인 줄 알고 누르는 일이 생긴다.
        var owner = !string.IsNullOrWhiteSpace(t.CreId)
                    && !string.Equals(t.CreId, me, StringComparison.OrdinalIgnoreCase)
            ? $"\n{t.CreId} 님이 적은 건입니다."
            : string.Empty;

        var title = t.Title is { Length: > 0 } name ? name : $"지시 #{t.TaskKey}";

        var ok = await _confirm.AskAsync(
            $"「{title}」 을(를) 지웁니다.{owner}\n아직 요청하지 않은 건이라 적어 둔 글만 사라집니다.",
            title: "작성중인 지시 삭제");

        if (!ok)
        {
            return;
        }

        _deleting = true;

        try
        {
            await Api.DeleteAsync(t.TaskKey);
        }
        catch (ApiException ex)
        {
            Toasts.Show($"지우지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }
        finally
        {
            _deleting = false;
        }

        Toasts.Show("지웠습니다.");

        try
        {
            Drafts.Use(me);
            await Drafts.ReadAsync();
            await Drafts.RemoveAsync(t.TaskKey);

            // 이어서 지시에 적다 만 것도 같이 버린다 — 건이 없어졌으므로
            // 되살릴 자리가 없다.
            ContinueDrafts.Use(me);
            await ContinueDrafts.ClearAsync(null, t.TaskKey);
        }
        catch
        {
            // 임시본을 못 치워도 지운 것은 지운 것이다 — 이 한 조각 때문에
            // 「지웠습니다」를 뒤집지 않는다.
        }

        await OnDeleted.InvokeAsync(t);
    }

    private void GoDetail(long taskKey) => Nav.NavigateTo($"/projmng/ai/tasks?task={taskKey}");
}
