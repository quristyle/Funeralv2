using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class ScheduleNotePopup : IDisposable
{
    [Inject] private ScheduleNoteReveal Reveal { get; set; } = default!;
    [Inject] private WbsClient Api { get; set; } = default!;
    [Inject] private ScheduleNoteAssist Assist { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private IServiceScopeFactory ScopeFactory { get; set; } = default!;
    [Inject] private ILogger<ScheduleNotePopup> Logger { get; set; } = default!;

    private const string ProjectStorageKeyPrefix = "jsini-snote-prj:";
    private string ProjectStorageKey =>
        $"{ProjectStorageKeyPrefix}{(string.IsNullOrWhiteSpace(Me?.Username) ? "?" : Me.Username.Trim())}";

    private const string DefaultKind = "Public";

    private string? _projectCode;
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

    private string? _body;
    private string? _addTarget;
    private bool _storageReady;

    protected override void OnInitialized()
    {
        Reveal.Changed += OnRevealChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var remembered = await ReadRememberedProjectAsync();
                if (!string.IsNullOrWhiteSpace(remembered) && string.IsNullOrWhiteSpace(_projectCode))
                {
                    _projectCode = remembered;
                    StateHasChanged();
                }
            }
            finally
            {
                _storageReady = true;
            }
        }
    }

    private void OnRevealChanged()
    {
        if (Reveal.IsOpen)
        {
            if (string.IsNullOrWhiteSpace(_projectCode) && _storageReady)
            {
                _ = RestoreProjectAsync();
            }
        }
        InvokeAsync(StateHasChanged);
    }

    private async Task RestoreProjectAsync()
    {
        var remembered = await ReadRememberedProjectAsync();
        if (!string.IsNullOrWhiteSpace(remembered))
        {
            _projectCode = remembered;
            await InvokeAsync(StateHasChanged);
        }
    }

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

    private async Task RememberProjectAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", ProjectStorageKey, code);
        }
        catch (JSDisconnectedException) { }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }

    private Task OnVisibleChangedAsync(bool visible)
    {
        if (!visible)
        {
            Close();
        }
        else
        {
            Reveal.Open();
        }
        return Task.CompletedTask;
    }

    private void Close()
    {
        Reveal.Close();
    }

    private Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 본문 작성창에서 Ctrl+Enter(또는 Mac의 Cmd+Enter) 입력 시 저장을 실행한다.
    /// </summary>
    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter" && (e.CtrlKey || e.MetaKey))
        {
            await SaveAsync();
        }
    }

    private Task SaveAsync()
    {
        if (!int.TryParse(_projectCode, out var prjRid))
        {
            Toasts.Show("프로젝트를 고르십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_body))
        {
            Toasts.Show("본문을 입력하십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        var body = _body.Trim();
        var prjCode = _projectCode;
        var addTarget = string.IsNullOrWhiteSpace(_addTarget) ? null : _addTarget.Trim();
        var currentUserName = !string.IsNullOrWhiteSpace(Me?.DisplayName)
            ? Me.DisplayName.Trim()
            : (!string.IsNullOrWhiteSpace(Me?.Username) ? Me.Username.Trim() : null);

        // 프로젝트 선택 기억
        _ = RememberProjectAsync(prjCode);

        // 즉시 본문 초기화 및 팝업 닫기 (화면에서 대기 없이 즉시 닫힘)
        _body = null;
        _addTarget = null;
        Reveal.Close();

        Toasts.Show("일정 메모 저장을 요청했습니다. 백그라운드에서 분석 후 등록됩니다.");

        // 백엔드/백그라운드에서 비동기 처리
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = ScopeFactory.CreateScope();
                var Assist = scope.ServiceProvider.GetRequiredService<ScheduleNoteAssist>();
                var Api = scope.ServiceProvider.GetRequiredService<WbsClient>();

                IReadOnlyList<ScheduleDraft> drafts = [];
                try
                {
                    drafts = await Assist.DraftManyAsync(body, currentUserName, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "ScheduleNoteAssist 분석 중 오류: 기본값으로 일정 등록 진행");
                }

                // AI 가 실패해도 메모는 버리지 않는다 — 기본값 한 건으로라도 남긴다.
                if (drafts.Count == 0)
                {
                    drafts = [new ScheduleDraft(null, null, null, null, null, currentUserName, null, null, null)];
                }

                var saved = new List<string>();
                Exception? lastError = null;

                foreach (var draft in drafts)
                {
                    var item = ToItem(draft, prjRid, body, addTarget, currentUserName);
                    try
                    {
                        await Api.CreateAsync(item);
                        saved.Add(item.ProcNm ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        Logger.LogError(ex, "일정 백그라운드 등록 실패 — {Title}", item.ProcNm);
                    }
                }

                if (saved.Count == 0)
                {
                    throw lastError ?? new InvalidOperationException("저장한 일정이 없습니다.");
                }

                if (saved.Count == 1 && lastError is null)
                {
                    Toasts.Show($"일정에 저장했습니다 — {saved[0]}");
                }
                else if (lastError is null)
                {
                    Toasts.Show($"일정 {saved.Count}건으로 나누어 저장했습니다 — {string.Join(", ", saved)}");
                }
                else
                {
                    Toasts.Show($"일정 {drafts.Count}건 중 {saved.Count}건만 저장했습니다 — {lastError.Message}", NoticeTone.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "일정 백그라운드 등록 실패");
                Toasts.Show($"일정을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>뽑은 일정 한 건을 저장할 줄로 만든다. 원문(BodyText)은 갈라진 일정마다 메모 전체를 그대로 싣는다.</summary>
    private static WbsItemDto ToItem(ScheduleDraft draft, int prjRid, string body, string? addTarget, string? currentUserName)
    {
        var startDate = draft.Start ?? AppTime.Today;
        var endDate = draft.End is { } e && e >= startDate ? e : startDate;

        var startTime = draft.StartTime ?? TimeOnly.MinValue;
        var endTime = draft.EndTime ?? draft.StartTime ?? TimeOnly.MinValue;

        var startDt = draft.StartDateTime ?? startDate.ToDateTime(startTime);
        var endDt = draft.EndDateTime ?? (draft.End is not null ? endDate.ToDateTime(endTime) : startDt);
        if (endDt < startDt) endDt = startDt;

        var title = draft.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            var lines = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            title = lines.Length > 0 ? lines[0] : "AI 일정";
            if (title.Length > 30) title = title[..30];
        }

        var summary = draft.Summary?.Trim() ?? string.Empty;

        return new WbsItemDto
        {
            PrjRid = prjRid,
            ScheduleType = DefaultKind,
            ProcNm = title,
            PlanSdt = startDt,
            PlanEdt = endDt,
            ReqUser = draft.Requester?.Trim(),
            DevUser = !string.IsNullOrWhiteSpace(draft.Assignee) ? draft.Assignee.Trim() : currentUserName,
            Summary = summary,
            Comm = summary,
            BodyText = body,
            AddTarget = addTarget
        };
    }

    public void Dispose()
    {
        Reveal.Changed -= OnRevealChanged;
    }
}
