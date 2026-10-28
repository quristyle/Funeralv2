using Microsoft.AspNetCore.Components;
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
    private bool _saving;
    private bool _storageReady;
    private CancellationTokenSource? _cts;

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
        _saving = false;
        _cts?.Cancel();
        Reveal.Close();
    }

    private Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (!int.TryParse(_projectCode, out var prjRid))
        {
            Toasts.Show("프로젝트를 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_body))
        {
            Toasts.Show("본문을 입력하십시오.", NoticeTone.Warning);
            return;
        }

        _saving = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            await InvokeAsync(StateHasChanged);

            ScheduleDraft? draft = null;
            try
            {
                draft = await Assist.DraftAsync(_body, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // AI 호출 실패 시 본문 기반으로 기본 일정 생성
            }

            if (ct.IsCancellationRequested) return;

            var today = AppTime.Today;
            var start = draft?.Start ?? today;
            var end = draft?.End is { } e && e >= start ? e : start;

            var title = draft?.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                var lines = _body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                title = lines.Length > 0 ? lines[0] : "AI 일정";
                if (title.Length > 30) title = title[..30];
            }

            var summary = draft?.Summary?.Trim() ?? string.Empty;

            var item = new WbsItemDto
            {
                PrjRid = prjRid,
                ScheduleType = DefaultKind,
                ProcNm = title,
                PlanSdt = start.ToDateTime(TimeOnly.MinValue),
                PlanEdt = end.ToDateTime(TimeOnly.MinValue),
                Summary = summary,
                BodyText = _body.Trim()
            };

            await Api.CreateAsync(item, ct);

            _ = RememberProjectAsync(_projectCode);
            Toasts.Show("일정에 저장했습니다.");

            _body = null;
            Reveal.Close();
        }
        catch (OperationCanceledException)
        {
            // 사용자가 닫았거나 취소됨
        }
        catch (Exception ex)
        {
            Toasts.Show($"일정을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _saving = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        Reveal.Changed -= OnRevealChanged;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
