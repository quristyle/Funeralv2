using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using JSini.Web.Http;
using JSini.Web.Components.Data;
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
    [Inject] private AiDashboardClient Dashboard { get; set; } = default!;

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

    /// <summary>
    /// 상세 화면을 닫는다. 모바일 환경의 하단 단추 줄 등에서 닫기 단추가 눌렸을 때 불린다.
    /// </summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private bool _retryOpen;
    private bool _retrying;
    private string? _retryAddition;
    private string? _retryError;

    private AiTaskContinuePopup? _continuePopup;
    private IReadOnlyList<AiModelOption> _kinds = [];
    private bool _kindsRead;

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

        if (!_kindsRead)
        {
            // 목록을 못 읽어도 창은 연다 — 그때는 지난 회차의 AI 한 줄만 선다
            // (<see cref="RetryKinds"/>). 「이어서 지시」 창과 같은 판단이다.
            _kinds = await ModelCodes.GetAsync();
            _kindsRead = _kinds.Count > 0;
        }

        // **한도도 함께 읽는다.** 이 창을 여는 가장 잦은 까닭이 「한도가 차서
        // 실패했다」라, 갈아탈 AI 를 고르는 자리에서 남은 한도를 못 보면
        // 방금 막힌 그 AI 에게 그대로 다시 보내게 된다. 통은 「이어서 지시」와
        // 하나다 — 한 창에서 읽어 두면 다른 창이 다시 읽지 않는다.
        if (!_usageSeen)
        {
            await LoadUsageAsync();
        }

        _retryKind = MatchKindCase(Item?.RunnerKind);

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

    /// <summary>
    /// 칸 아래 한 줄. <b>갈아탔는지를 말한다</b> — 「이어서 지시」( <see cref="AiTaskContinuePopup"/> )와
    /// 같은 갈래이고 같은 차례로 묻는다.
    /// </summary>
    private string RetryKindNote
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

    /// <summary>
    /// 지난 회차가 <b>어떻게 끝났나</b>. 창 머리 아래 한 줄로 선다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「이어서 지시」 창에는 이 줄이 없다 — 그쪽은 <b>끝난 건</b>에만 서므로
    /// 적을 것이 「잘 끝났다」뿐이다. 재시도 창은 반대다. <b>여기 오는 건은
    /// 모두 실패했고</b>, 덧붙일 말을 정하는 값이 바로 그 실패의 꼴이다 —
    /// 시간이 모자랐는지, 한도가 찼는지, 애초에 집혀 가지 않았는지.
    /// </para>
    /// <para>
    /// 그 값을 보려고 <b>창을 닫고 상세로 갔다 다시 여는</b> 일이 잦았다.
    /// 창이 「무엇을 적을까」를 묻는데 정작 그 판단의 근거가 창 밖에 있었다.
    /// </para>
    /// <para>
    /// <b>까닭은 끝에 한 번만, 줄여서 적는다.</b> 실행기가 적는 글은 로그
    /// 여러 줄이 통째로 들어오기도 해서, 그대로 두면 이 한 줄이 창을 밀어
    /// 내리고 글상자와 단추가 자판 밑으로 내려간다. 온전한 글은 「자세히
    /// 보기」가 들고 있다.
    /// </para>
    /// </remarks>
    private string RetryStatusNote
    {
        get
        {
            if (Item is not { } t)
            {
                return string.Empty;
            }

            var parts = new List<string> { t.StatusText };

            if (!string.IsNullOrWhiteSpace(t.RunnerKind))
            {
                parts.Add(KindName(t.RunnerKind));
            }

            // 몇 번까지 갔나. **자동 재시도를 다 쓴 것**이 이 창이 열리는
            // 조건이라(`AiTaskDto.CanManualRetry`), 그 수가 곧 「사람이
            // 손대야 하는 자리까지 왔다」는 뜻이다.
            if (t.AttemptMax > 0)
            {
                parts.Add($"{t.AttemptCount}/{t.AttemptMax}회");
            }

            if (t.FinishedAt is { } at)
            {
                parts.Add(at.Kst("MM-dd HH:mm"));
            }

            var head = $"지난 회차: {string.Join(" · ", parts)}";

            return Shorten(t.LastError) is { Length: > 0 } why ? $"{head} — {why}" : head;
        }
    }

    /// <summary>
    /// 실행기가 남긴 까닭을 <b>한 줄로</b> 줄인다. 줄바꿈을 가운뎃점으로 잇고
    /// 길면 자른다 — 로그가 통째로 들어와도 창이 안 밀린다.
    /// </summary>
    private static string Shorten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var one = string.Join(
            " · ",
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return one.Length > 140 ? one[..140] + "…" : one;
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

    private string? MatchKindCase(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return kind;
        return _kinds.FirstOrDefault(
            k => string.Equals(k.Value, kind, StringComparison.OrdinalIgnoreCase))?.Value ?? kind;
    }

    private IReadOnlyList<AiUsageSnapshot> _usage = [];
    private bool _usageSeen;

    private List<AiUsageSnapshot> UsageOf(string? kind)
        => string.IsNullOrWhiteSpace(kind)
            ? []
            : [.. _usage.Where(u => string.Equals(u.RunnerKind, kind, StringComparison.OrdinalIgnoreCase))];

    private async Task LoadUsageAsync()
    {
        _usage = await Dashboard.UsageAsync();
        _usageSeen = true;
    }

    private async Task OpenContinueModalAsync()
    {
        if (Item is { } t && _continuePopup is not null)
        {
            await _continuePopup.OpenAsync(t);
        }
    }

    private async Task OnContinueFinishedAsync(AiTaskDto updated)
    {
        if (updated is not null)
        {
            await OnChanged.InvokeAsync(updated);
        }
    }

    /// <summary>지금 로그인한 사람의 아이디. 임시본 열쇠와 「남의 건인가」에 쓴다.</summary>
    private async Task<string?> MeAsync() =>
        AuthState is null ? null : (await AuthState).User.Identity?.Name;

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
